# Авторизация, подписки и биллинг

## Важное продуктовое ограничение

Подписка ChatGPT и OpenAI API — разные продукты с отдельным биллингом. Наличие ChatGPT Free/Plus/Pro/Business само по себе не предоставляет стороннему приложению API-бюджет и не должно использоваться как entitlement для генерации в RHINO Minion.

RHINO Minion также не должен автоматически создавать или передавать пользователю OpenAI API key. Provider key является серверным секретом. Если пользователь покупает генерацию внутри RHINO Minion, он покупает подписку или внутренние кредиты RHINO Minion, после чего наш backend оплачивает и вызывает model API.

## Поддерживаемые коммерческие режимы

### Managed usage — основной режим

1. Пользователь создаёт аккаунт RHINO Minion.
2. Получает trial/free entitlement либо покупает план/credits.
3. Frontend отправляет prompt в RHINO Minion AI gateway.
4. Gateway атомарно резервирует внутренний usage budget.
5. Gateway вызывает модель собственным server-side provider key.
6. Фактический расход записывается в ledger; резерв корректируется или возвращается.
7. Валидированный geometry plan передаётся локальному bridge.

Пользователь никогда не видит provider key. Приложение выдаёт только собственные session/access tokens.

### BYOK — опциональный режим

Пользователь самостоятельно создаёт API key в своём API Platform account и явно подключает его к RHINO Minion. Ключ нельзя хранить в browser localStorage, логах или `.env` пользователя без шифрования. Предпочтительные варианты:

- хранение в OS credential vault и прямой вызов из локального backend;
- envelope encryption и хранение ciphertext в облаке с KMS-managed key;
- отказ от постоянного хранения с вводом на каждую сессию.

BYOK не использует и не проверяет ChatGPT-подписку: API-биллинг пользователя остаётся отдельным.

## Архитектура

```text
Rhino plugin ←localhost→ Local backend ←token→ RHINO Minion Cloud
                                             ├── Identity / OIDC
React frontend ───────────────────────token→ ├── Entitlements
                                             ├── Billing webhooks
                                             ├── Usage ledger
                                             └── AI gateway ──→ OpenAI API
```

### Identity

- OIDC/OAuth 2.1 Authorization Code + PKCE.
- Вход открывается в системном браузере.
- Callback возвращается через loopback redirect или зарегистрированный custom URI scheme.
- Короткоживущий JWT access token проверяется по issuer, audience, signature, expiry и nonce.
- Refresh token ротируется и хранится в системном credential vault.
- Поддерживаются revoke/logout и список активных устройств.

До выбора провайдера реализация остаётся vendor-neutral. Возможные варианты оцениваются отдельно по desktop PKCE, MFA, passkeys, EU-регионам, стоимости MAU и webhook/API возможностям.

### Entitlements

Минимальная модель:

```text
User
├── status: active | blocked | deleted
├── plan: free | trial | pro
├── subscription_status
├── credit_balance (derived, not authoritative)
└── feature_flags

UsageLedgerEntry
├── id
├── user_id
├── generation_id
├── type: grant | reserve | capture | release | refund | adjustment
├── amount
├── provider_usage
└── created_at
```

Баланс вычисляется из ledger. Для одной генерации используется уникальный idempotency key. Резервирование должно выполняться транзакционно, чтобы несколько запросов не могли потратить один баланс.

### Billing

- Использовать hosted checkout, не собирать данные банковской карты самостоятельно.
- Источником истины служат подписанные billing webhooks, а не redirect после оплаты.
- Сохранять provider customer/subscription/event IDs.
- Повторный webhook не должен повторно начислять credits.
- Предоставить customer portal для карты, счетов и отмены подписки.
- Отдельно определить налоги/VAT, валюты, возвраты, chargebacks и географию продаж.

### AI gateway

- OpenAI API key хранится только в cloud secret manager.
- Каждый запрос связан с внутренним user ID и generation ID.
- Проверяются entitlement, rate limit, размер scene context и доступные tools.
- Вызов использует ограниченный model allowlist и максимальный token budget.
- В usage ledger записываются model, input/output tokens, стоимость и статус.
- В клиент возвращается geometry plan, но не provider credentials или необработанные внутренние ошибки.

## Что нельзя обещать пользователю

- «Войдите через ChatGPT Plus и используйте его лимит в RHINO Minion».
- «Мы автоматически создадим вам личный OpenAI API key».
- «ChatGPT credits являются API credits».
- «Ключ безопасно хранится внутри React/Tauri/Rhino bundle».

Корректная формулировка:

> Войдите в аккаунт RHINO Minion. Активная подписка или купленные RHINO Minion credits дают доступ к генерации. OpenAI API вызывается защищённым сервером RHINO Minion. Опционально пользователь может подключить собственный API key в BYOK-режиме.

## API-контуры

```text
POST /v1/auth/device/start
POST /v1/auth/device/exchange
POST /v1/auth/refresh
POST /v1/auth/logout
GET  /v1/me
GET  /v1/entitlements
POST /v1/billing/checkout
POST /v1/billing/portal
POST /v1/billing/webhook
POST /v1/generations
GET  /v1/generations/{id}
GET  /v1/usage
```

Фактические auth endpoints могут быть делегированы выбранному OIDC-провайдеру. Billing webhook никогда не вызывается desktop-клиентом.

## Этапы внедрения

1. Выбрать identity и billing providers, юридическую модель продаж и тарифы.
2. Ввести cloud control plane отдельно от localhost backend.
3. Реализовать login PKCE, token vault и `/me`.
4. Добавить entitlement middleware и Free/Trial feature flags.
5. Реализовать hosted checkout, webhooks и subscription state machine.
6. Добавить транзакционный usage ledger и credit reservations.
7. Перенести production OpenAI calls в cloud AI gateway.
8. Добавить rate limits, abuse controls, audit и observability.
9. При необходимости добавить BYOK как отдельную настройку.
10. Провести threat modeling, penetration test и проверку privacy/terms перед продажами.

## Критерии готовности

- Ни один provider key не присутствует в shipped artifacts или browser traffic.
- Покупка активирует entitlement только после валидного webhook.
- Повтор webhook или generation request не удваивает списание/начисление.
- Logout/revoke блокирует новые cloud jobs.
- Исчерпание quota предсказуемо запрещает новую генерацию.
- Ошибка model API освобождает зарезервированные credits.
- Пользователь видит план, баланс, историю использования и управление подпиской.
- Удаление аккаунта и экспорт данных соответствуют принятой privacy policy.
