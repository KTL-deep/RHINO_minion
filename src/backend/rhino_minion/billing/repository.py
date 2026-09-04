import sqlite3
from decimal import Decimal
from pathlib import Path

from rhino_minion.billing.models import Order, OrderStatus
from rhino_minion.config import settings


class BillingRepository:
    def __init__(self, database_path: str | None = None) -> None:
        self._path = Path(database_path or settings.billing_database)
        self._path.parent.mkdir(parents=True, exist_ok=True)
        self._initialize()

    def create_order(self, order: Order) -> None:
        with self._connect() as connection:
            connection.execute(
                """INSERT INTO billing_orders
                   (id, user_id, product, amount, currency, credits, status, payment_id)
                   VALUES (?, ?, ?, ?, ?, ?, ?, ?)""",
                (
                    order.id,
                    order.user_id,
                    order.product,
                    str(order.amount),
                    order.currency,
                    order.credits,
                    order.status.value,
                    order.payment_id,
                ),
            )

    def attach_payment(self, order_id: str, payment_id: str, status: OrderStatus) -> None:
        with self._connect() as connection:
            connection.execute(
                "UPDATE billing_orders SET payment_id = ?, status = ? WHERE id = ?",
                (payment_id, status.value, order_id),
            )

    def get_order(self, order_id: str) -> Order | None:
        with self._connect() as connection:
            row = connection.execute(
                "SELECT * FROM billing_orders WHERE id = ?", (order_id,)
            ).fetchone()
        return self._to_order(row) if row else None

    def get_order_by_payment(self, payment_id: str) -> Order | None:
        with self._connect() as connection:
            row = connection.execute(
                "SELECT * FROM billing_orders WHERE payment_id = ?", (payment_id,)
            ).fetchone()
        return self._to_order(row) if row else None

    def settle_order(self, order_id: str) -> bool:
        with self._connect() as connection:
            connection.execute("BEGIN IMMEDIATE")
            cursor = connection.execute(
                """UPDATE billing_orders SET status = ?
                   WHERE id = ? AND status != ?""",
                (OrderStatus.SUCCEEDED.value, order_id, OrderStatus.SUCCEEDED.value),
            )
            if cursor.rowcount == 1:
                order = connection.execute(
                    "SELECT user_id, credits FROM billing_orders WHERE id = ?", (order_id,)
                ).fetchone()
                connection.execute(
                    """INSERT OR IGNORE INTO credit_ledger
                       (source_id, user_id, amount, kind) VALUES (?, ?, ?, ?)""",
                    (f"order:{order_id}", order["user_id"], order["credits"], "purchase"),
                )
            return cursor.rowcount == 1

    def mark_canceled(self, order_id: str) -> None:
        with self._connect() as connection:
            connection.execute(
                """UPDATE billing_orders SET status = ?
                   WHERE id = ? AND status != ?""",
                (OrderStatus.CANCELED.value, order_id, OrderStatus.SUCCEEDED.value),
            )

    def credit_balance(self, user_id: str) -> int:
        with self._connect() as connection:
            row = connection.execute(
                """SELECT COALESCE(SUM(amount), 0) AS balance
                   FROM credit_ledger WHERE user_id = ?""",
                (user_id,),
            ).fetchone()
        return int(row["balance"])

    def consume_credit(self, user_id: str, generation_id: str) -> bool:
        with self._connect() as connection:
            connection.execute("BEGIN IMMEDIATE")
            balance = connection.execute(
                "SELECT COALESCE(SUM(amount), 0) FROM credit_ledger WHERE user_id = ?",
                (user_id,),
            ).fetchone()[0]
            if int(balance) < 1:
                return False
            connection.execute(
                """INSERT INTO credit_ledger (source_id, user_id, amount, kind)
                   VALUES (?, ?, -1, 'generation')""",
                (f"generation:{generation_id}", user_id),
            )
            return True

    def release_credit(self, user_id: str, generation_id: str) -> None:
        with self._connect() as connection:
            connection.execute(
                """INSERT OR IGNORE INTO credit_ledger (source_id, user_id, amount, kind)
                   VALUES (?, ?, 1, 'release')""",
                (f"release:{generation_id}", user_id),
            )

    def _initialize(self) -> None:
        with self._connect() as connection:
            connection.execute(
                """CREATE TABLE IF NOT EXISTS billing_orders (
                    id TEXT PRIMARY KEY,
                    user_id TEXT NOT NULL,
                    product TEXT NOT NULL,
                    amount TEXT NOT NULL,
                    currency TEXT NOT NULL,
                    credits INTEGER NOT NULL,
                    status TEXT NOT NULL,
                    payment_id TEXT UNIQUE
                )"""
            )
            connection.execute(
                """CREATE TABLE IF NOT EXISTS credit_ledger (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    source_id TEXT NOT NULL UNIQUE,
                    user_id TEXT NOT NULL,
                    amount INTEGER NOT NULL,
                    kind TEXT NOT NULL,
                    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                )"""
            )

    def _connect(self) -> sqlite3.Connection:
        connection = sqlite3.connect(self._path)
        connection.row_factory = sqlite3.Row
        return connection

    @staticmethod
    def _to_order(row: sqlite3.Row) -> Order:
        return Order(
            id=row["id"],
            user_id=row["user_id"],
            product=row["product"],
            amount=Decimal(row["amount"]),
            currency=row["currency"],
            credits=row["credits"],
            status=OrderStatus(row["status"]),
            payment_id=row["payment_id"],
        )
