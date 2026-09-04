from decimal import Decimal, InvalidOperation
from functools import lru_cache
from typing import Any
from uuid import uuid4

from fastapi import APIRouter, Depends, HTTPException, status

from rhino_minion.auth import CurrentUser, require_user
from rhino_minion.billing.catalog import get_product
from rhino_minion.billing.models import (
    CheckoutRequest,
    CheckoutResponse,
    EntitlementResponse,
    Order,
    OrderStatus,
)
from rhino_minion.billing.repository import BillingRepository
from rhino_minion.billing.yookassa import YooKassaClient, YooKassaError

router = APIRouter(prefix="/api/billing", tags=["billing"])


@lru_cache(maxsize=1)
def repository() -> BillingRepository:
    return BillingRepository()


@router.post("/checkout", response_model=CheckoutResponse)
async def create_checkout(
    request: CheckoutRequest,
    user: CurrentUser = Depends(require_user),
) -> CheckoutResponse:
    product = get_product(request.product)
    if product is None:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail="Product is unavailable or has no configured price",
        )

    order = Order(
        id=str(uuid4()),
        user_id=user.id,
        product=product.code,
        amount=product.amount,
        currency="RUB",
        credits=product.credits,
        status=OrderStatus.CREATED,
    )
    repository().create_order(order)
    try:
        payment = await YooKassaClient().create_payment(
            order_id=order.id,
            user_id=user.id,
            amount=order.amount,
            description=product.title,
            product=product.code,
        )
        payment_id = str(payment["id"])
        confirmation_url = str(payment["confirmation"]["confirmation_url"])
    except (KeyError, TypeError, YooKassaError) as exception:
        raise HTTPException(
            status_code=502,
            detail="Could not create YooKassa payment",
        ) from exception

    repository().attach_payment(order.id, payment_id, OrderStatus.PENDING)
    return CheckoutResponse(
        order_id=order.id,
        payment_id=payment_id,
        confirmation_url=confirmation_url,
        status=OrderStatus.PENDING,
    )


@router.get("/entitlements", response_model=EntitlementResponse)
def entitlements(user: CurrentUser = Depends(require_user)) -> EntitlementResponse:
    return EntitlementResponse(user_id=user.id, credits=repository().credit_balance(user.id))


@router.post("/yookassa/webhook")
async def yookassa_webhook(notification: dict[str, Any]) -> dict[str, bool]:
    payment_id = str(notification.get("object", {}).get("id", ""))
    if not payment_id:
        return {"accepted": True}

    order = repository().get_order_by_payment(payment_id)
    if order is None:
        return {"accepted": True}

    try:
        payment = await YooKassaClient().get_payment(payment_id)
        _verify_payment(payment, order)
    except YooKassaError as exception:
        raise HTTPException(
            status_code=502,
            detail="Could not verify YooKassa payment",
        ) from exception

    payment_status = payment.get("status")
    if payment_status == "succeeded" and payment.get("paid") is True:
        repository().settle_order(order.id)
    elif payment_status == "canceled":
        repository().mark_canceled(order.id)
    return {"accepted": True}


def _verify_payment(payment: dict[str, Any], order: Order) -> None:
    metadata = payment.get("metadata") or {}
    amount = payment.get("amount") or {}
    try:
        actual_amount = Decimal(str(amount.get("value")))
    except (InvalidOperation, TypeError) as exception:
        raise YooKassaError("Payment has an invalid amount") from exception

    matches = (
        str(payment.get("id")) == order.payment_id
        and metadata.get("order_id") == order.id
        and metadata.get("user_id") == order.user_id
        and metadata.get("product") == order.product
        and actual_amount == order.amount
        and amount.get("currency") == order.currency
    )
    if not matches:
        raise YooKassaError("Payment does not match the local order")
