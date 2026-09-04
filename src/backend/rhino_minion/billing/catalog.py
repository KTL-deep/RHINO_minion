from dataclasses import dataclass
from decimal import Decimal

from rhino_minion.config import settings


@dataclass(frozen=True)
class Product:
    code: str
    title: str
    amount: Decimal
    credits: int


def get_product(code: str) -> Product | None:
    if code != "credits_100" or settings.credits_100_price_rub <= 0:
        return None
    return Product(
        code="credits_100",
        title="RHINO Minion — 100 generation credits",
        amount=Decimal(settings.credits_100_price_rub),
        credits=100,
    )
