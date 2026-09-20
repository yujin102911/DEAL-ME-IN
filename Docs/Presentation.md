# 보상 연출

- HIT/DEAL: 결과를 계산하기 전 중립적인 카드 뒷면이 들어온 뒤 공개됩니다. 연출은 카드 RNG를 사용하지 않습니다.
- 성장한 합계 적중: 합계가 튀어 오르고 숫자판에서 테이블로 빛 토큰이 이동합니다.
- STAND: 기본 점수 → 성장 → 희귀도 → 연속 성공 순서로 정산하고, 최종 점수를 목표 게이지에 반영합니다. 최종 표시에는 TableRun의 실제 결과값을 사용합니다.
- 이번 런의 이전 최고 핸드 점수를 넘으면 기록 배지와 입자 연출이 나옵니다. 새 런에서 기록이 초기화됩니다.
- 핸드 코인은 지갑으로 이동합니다. 클리어 기본 보상과 남은 핸드 보너스는 따로 보여줍니다.
- 딜러 성장·해금은 숫자판으로, 카드 추가는 덱으로 이동합니다. 제거는 카드가 두 조각으로 갈라집니다. 핸드 추가는 남은 핸드 표시로 이동합니다.
- 코인 카드의 선택 비용과 코인 지급은 별도로 표시합니다.
- 연출 중 플레이 입력을 잠그며 Space/Escape 또는 ‘연출 건너뛰기’로 빠르게 마칠 수 있습니다. 건너뛰기는 다음 핸드 진행을 의미하지 않습니다.

## 검증

Unity 6000.3.24f1에서 기존 딜러 138개·핵심 규칙 89개 검사와 연출 89개 검사 통과. 시드별 카드 순서, 연타, 건너뛰기, 보상 중복 방지, Ace 버스트 보류, 클리어 보너스, 6종 딜러 효과, 새 런 취소를 확인했습니다. 실제 UI STAND 클릭으로 단계별 카운트와 종료 후 지갑/점수/입력 복구를 확인했습니다.

- `PresentationPlayChecks.Start()`는 Play 모드에서 연출 회귀 검사를 실행합니다. 테스트 중 임시로 백그라운드 실행을 허용하고 종료 후 복구합니다.
- `PresentationChecks.Run()`은 배치 실행용 규칙 검사이며 검사 후 에디터를 종료합니다.
- `Tools > Number Table > Preview Presentation`은 플레이 씬을 열고 MCP 브리지와 Play 모드를 시작합니다.
- 한글 프로젝트 경로에서 MCP 서버를 사용할 때 Python 서버 프로세스에 `PYTHONUTF8=1` 환경 변수가 필요할 수 있습니다.

첫 연출 작업에서는 규칙을 유지했고, 두 번째 작업에서는 요청에 따라 합계 2를 추가했습니다. 연출 체감과 사운드의 취향 조정은 플레이 피드백에 따라 진행합니다.

## Second presentation pass
- Verification: Unity Play mode passed 138 dealer, 92 core and 96 presentation checks (326 total); console had no errors or warnings. The unskipped sequence checks score slam, goal stamp, coin tokens and deferred clear payout. Captured frames verified the score overlay and the A(1)+A(1)=2 result layout.
- Final score: side dimming, gathering light, brief silence, score slam and table-only shake.
- Layered ascending score chords, bass impact and metallic coin ticks; mute/skip honored.
- Up to eight staggered coin tokens; exact wallet balance, outgoing purchase direction.
- Crossing the stage target triggers gauge sparks, illuminated table border and a goal stamp immediately. Clear coins still pay only on Continue.
- Total 2 is initially safe and growable: base 320, rarity 5. Total 3 remains base 260. Both manual low aces are valid; auto still maximizes a safe total.
