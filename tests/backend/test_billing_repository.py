from decimal import Decimal

from rhino_minion.billing.models import Order, OrderStatus
from rhino_minion.billing.repository import BillingRepository


def test_settlement_and_consumption_are_idempotent(tmp_path) -> None:
    repository = BillingRepository(str(tmp_path / "billing.db"))
    order = Order(
        id="order-1",
        user_id="user-1",
        product="credits_100",
        amount=Decimal("500"),
        currency="RUB",
        credits=100,
        status=OrderStatus.CREATED,
        payment_id=None,
    )
    repository.create_order(order)
    repository.attach_payment(order.id, "payment-1", OrderStatus.PENDING)

    assert repository.settle_order(order.id) is True
    assert repository.settle_order(order.id) is False
    assert repository.credit_balance(order.user_id) == 100
    assert repository.consume_credit(order.user_id, "generation-1") is True
    assert repository.credit_balance(order.user_id) == 99

    repository.release_credit(order.user_id, "generation-1")
    repository.release_credit(order.user_id, "generation-1")
    assert repository.credit_balance(order.user_id) == 100
