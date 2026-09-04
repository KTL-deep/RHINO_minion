from decimal import Decimal
from typing import Any

import httpx

from rhino_minion.config import settings


class YooKassaError(RuntimeError):
    pass


class YooKassaClient:
    _base_url = "https://api.yookassa.ru/v3"

    def _credentials(self) -> tuple[str, str]:
        if not settings.yookassa_shop_id or not settings.yookassa_secret_key:
            raise YooKassaError("YooKassa credentials are not configured")
        return settings.yookassa_shop_id, settings.yookassa_secret_key

    async def create_payment(
        self,
        *,
        order_id: str,
        user_id: str,
        amount: Decimal,
        description: str,
        product: str,
    ) -> dict[str, Any]:
        body = {
            "amount": {"value": f"{amount:.2f}", "currency": "RUB"},
            "capture": True,
            "confirmation": {
                "type": "redirect",
                "return_url": settings.yookassa_return_url,
            },
            "description": description[:128],
            "metadata": {"order_id": order_id, "user_id": user_id, "product": product},
            "save_payment_method": False,
        }
        async with httpx.AsyncClient(timeout=30) as client:
            response = await client.post(
                f"{self._base_url}/payments",
                auth=self._credentials(),
                headers={"Idempotence-Key": order_id},
                json=body,
            )
        return self._parse(response)

    async def get_payment(self, payment_id: str) -> dict[str, Any]:
        async with httpx.AsyncClient(timeout=20) as client:
            response = await client.get(
                f"{self._base_url}/payments/{payment_id}",
                auth=self._credentials(),
            )
        return self._parse(response)

    @staticmethod
    def _parse(response: httpx.Response) -> dict[str, Any]:
        if response.is_error:
            raise YooKassaError(f"YooKassa API returned status {response.status_code}")
        payload = response.json()
        if not isinstance(payload, dict):
            raise YooKassaError("YooKassa API returned an invalid response")
        return payload
