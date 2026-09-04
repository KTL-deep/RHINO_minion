from decimal import Decimal
from enum import StrEnum

from pydantic import Field

from rhino_minion.models import StrictModel


class OrderStatus(StrEnum):
    CREATED = "created"
    PENDING = "pending"
    SUCCEEDED = "succeeded"
    CANCELED = "canceled"


class CheckoutRequest(StrictModel):
    product: str = Field(pattern=r"^[a-z0-9_]{1,40}$")


class CheckoutResponse(StrictModel):
    order_id: str
    payment_id: str
    confirmation_url: str
    status: OrderStatus


class Order(StrictModel):
    id: str
    user_id: str
    product: str
    amount: Decimal
    currency: str
    credits: int
    status: OrderStatus
    payment_id: str | None = None


class EntitlementResponse(StrictModel):
    user_id: str
    credits: int
