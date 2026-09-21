# Native Aion and Luna/Quna shops disabled

Launcher 2.5.42 disables the native Aion/Gameforge shop and Luna/Quna market
at every launch, including when an older external `launcher.json` is retained.
`GameLauncher.BuildLaunchCommand` removes `-shop`, `-ingameshop` and
`-ingamewebshop`, then adds exactly one `-dnpshop -dingameshop` pair.
The policy runs before credentials are appended and preserves unrelated options.

This uses the client's native feature switches. No executable, UI PAK, texture,
server configuration, account, or database is modified. Client release 2.4.6 and
all its packages remain unchanged. The website shop and player personal shops
are outside this change. Server mixed-faction commit `960f2be` remains canonical.

## Native evidence (Classic 2.4 x64)

Read-only inspection of the distributed `bin64/aionclassic.bin` and `Game.dll`:

- `-dnpshop` sets bit 75 (`0x4b`), the native market disable flag. The parser
  sets bit 11 of the high 64-bit word at RVA `0x143e2`.
- `-dingameshop` sets bit 78 (`0x4e`), the native ingame shop disable flag,
  at RVA `0x14439` (bit 14 of the high word).
- `-ingamewebshop` enables bit 46 (`0x2e`) at RVA `0x1460d`; it must be absent.
- `Game.dll` checks bit 75 in the market availability function at RVA
  `0x0f35b0`. Disabled returns false. Both the radar click handler (RVA
  `0x2b727a`) and the service menu handler (RVA `0x2f3cb5`) call this gate.
- Radar initialization checks the same bit before hiding `btn_quna_shop`
  and its background (RVA `0x2b2034` and `0x2b2096`).
- The web-shop service menu handler checks bit 46 at RVA `0x2f3c30` and
  does not open the web dialog when it is absent.

Game.dll SHA-256:
`f259b60f74768c226eafff085551700bcaf3aa43a20ede7fe3925e0e31daaf0f`.
Names alone were not considered proof of CVar behavior: `g_spinel_shop` and
`g_allowWebservice` were investigated but are not changed. In particular,
`g_test5` is a shared debug variable and is not a safe shop-specific switch.

## Validation and recipe

Regression tests cover stale enabling options, mixed case, duplicate disable
options, preservation of IP/port/language and other flags, idempotence, and
non-mutation of the stored profile. Windows CI builds the launcher and runs
the full test suite before release assets are published.

Manual in-game recipe after updating the launcher and fully restarting the game:

1. On `D:/games/aioncl-recette`, enter the existing character normally.
2. Confirm the gift/shop and purple Luna/Quna icons and their backgrounds are
   absent around the radar, in both HUD layouts if used.
3. Check the service menu and any previously assigned shop shortcuts: neither
   shop opens, and no Gameforge connection error appears in chat.
4. Confirm normal merchants, personal shops and the remaining HUD controls work.
5. Restart once more; the shops must remain disabled. A client Verify/Repair
   must not re-enable them because the policy is enforced by the launcher.

Runtime and visual results must be recorded separately; static native analysis
and unit tests do not constitute an in-game visual acceptance.
