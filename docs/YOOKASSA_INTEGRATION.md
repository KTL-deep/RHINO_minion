# Интеграция ЮKassa и Keycloak

## Выбор

Для приёма российских карт выбран сервис ЮKassa. Для identity выбран self-hosted Keycloak.

ЮKassa предоставляет redirect checkout, банковские карты и другие российские способы оплаты, API с ключами идемпотентности, тестовый магазин, webhooks, возвраты и автоплатежи. Альтернативы CloudPayments и Robokassa также функциональны, но для первого продукта ЮKassa даёт наиболее прямой API-контур и официальную Python-интеграцию.

Keycloak — не платёжный сервис. Это identity provider: отдельный сервер, который регистрирует пользователей, проверяет пароль/MFA/social login и выдаёт подписанные access tokens. RHINO Minion проверяет токен и получает стабильный `user_id`, к которому привязываются покупки, кредиты и генерации.

Self-hosted Keycloak выбран, чтобы:

- не хранить пароли в собственном FastAPI-коде;
- использовать стандартные OIDC discovery/JWKS/JWT;
- применять Authorization Code + PKCE для desktop/browser;
- не зависеть от доступности зарубежного auth-SaaS для российских пользователей;
- позже подключить Яндекс ID/VK ID как внешние identity providers.

## Реализованный контур

```text
React frontend
├── OIDC redirect + PKCE → Keycloak
├── Bearer access token → FastAPI
└── Hosted checkout redirect → ЮKassa

FastAPI
├── проверка JWT по Keycloak JWKS
├── серверный каталог товаров
├── создание локального order
├── POST /v3/payments с Idempotence-Key
├── webhook → GET /v3/payments/{id} для проверки
└── идемпотентное начисление credit ledger
```

Endpoints:

```text
POST /api/billing/checkout
GET  /api/billing/entitlements
POST /api/billing/yookassa/webhook
POST /api/sessions/{session_id}/prompt
```

При `RHINO_MINION_AUTH_ENABLED=true` генерация требует валидный Keycloak JWT и один внутренний кредит. Кредит резервируется атомарно; при ошибке plan/bridge возвращается. При отключённой auth локальная разработка остаётся бесплатной.

## Локальный Keycloak

Конфигурация находится в `infra/keycloak/`. Realm включает:

- пользовательскую регистрацию;
- восстановление пароля;
- public OIDC client `rhino-minion-desktop`;
- Authorization Code flow;
- обязательный PKCE S256;
- localhost redirect URIs для Vite.

Запуск:

```powershell
Copy-Item .env.example .env
# Заменить KEYCLOAK_ADMIN_PASSWORD
docker compose --env-file .env -f infra/keycloak/docker-compose.yml up -d
```

Development realm не предназначен для production. Для production нужны PostgreSQL, TLS, reverse proxy, backups, email verification, SMTP, MFA/passkeys, ограниченная admin network и регулярное обновление Keycloak.

## Настройка тестового магазина ЮKassa

1. Зарегистрировать магазин и получить тестовые `shop_id` и `secret_key`.
2. Заполнить `.env`:

```dotenv
RHINO_MINION_AUTH_ENABLED=true
RHINO_MINION_YOOKASSA_SHOP_ID=...
RHINO_MINION_YOOKASSA_SECRET_KEY=...
RHINO_MINION_CREDITS_100_PRICE_RUB=500
RHINO_MINION_YOOKASSA_RETURN_URL=http://127.0.0.1:5173/?payment=return
```

3. Для production развернуть webhook на публичном HTTPS 443/8443:

```text
https://api.example.ru/api/billing/yookassa/webhook
```

4. Подписаться минимум на `payment.succeeded` и `payment.canceled`.
5. Пройти тесты успешной, отклонённой и повторно доставленной оплаты.

Цена никогда не принимается от frontend: клиент передаёт только product code, а сумма берётся из серверного каталога.

## Проверка webhook

ЮKassa webhook не используется как безусловное доказательство оплаты. Backend извлекает payment ID и повторно получает объект платежа через аутентифицированный ЮKassa API. Затем проверяются:

- payment ID;
- локальный order ID;
- user ID;
- product code;
- точная сумма и валюта;
- финальный status и `paid`.

Только после этого выполняется идемпотентное начисление. Поддельная или повторная доставка не должна увеличить баланс.

## Что ещё нужно до реальных платежей

- Российское ИП/ООО/самозанятый и одобрение магазина ЮKassa.
- Решение по 54-ФЗ, онлайн-кассе, составу чека, НДС и данным покупателя.
- Публичный cloud backend с HTTPS; localhost webhook не работает.
- Production PostgreSQL вместо SQLite.
- Политики оферты, конфиденциальности, возвратов и автоплатежей.
- Секреты в cloud secret manager.
- Фиксированный коммерческий каталог и unit economics API usage.
- Отдельное подключение автоплатежей у ЮKassa, согласие пользователя и scheduler повторных списаний.

Текущая реализация полностью покрывает одноразовую покупку credit pack в тестовом контуре. Рекуррентные списания намеренно не включены до согласования юридических условий, тарифа и активации функции магазином.
