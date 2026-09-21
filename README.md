# DEAL-ME-IN

트럼프 카드로 합계를 만들고, 덱과 합계별 점수를 성장시키는 싱글 플레이 로그라이크 프로토타입입니다.

## 다른 컴퓨터에서 실행

1. 이 저장소를 clone합니다.
2. Unity Hub에서 **Unity 6.3 LTS / 6000.3.24f1**을 설치합니다.
3. Hub의 Add 프로젝트로 저장소 루트(Assets, Packages, ProjectSettings가 있는 폴더)를 선택합니다.
4. 첫 실행의 패키지 다운로드와 임포트가 끝날 때까지 기다립니다.
5. `Assets/Scenes/NumberTable.unity`를 열고 Play를 누릅니다.

Windows에서 검증했습니다. 한글 UI는 OS 글꼴(맑은 고딕)을 사용하므로 다른 OS에서는 한글 글꼴 대응이 추가로 필요할 수 있습니다.

## 현재 게임

- 숫자판 강화: 고정 모양 블록 3개 중 1개를 골라 찍습니다. 회전은 없습니다. 열린 칸은 레벨 +1, 열쇠 표시 칸은 잠긴 숫자 하나를 해금합니다.
- 잠긴 숫자가 남으면 배치 가능한 열쇠 블록을 최소 1개 보장합니다. 숫자판 강화 다음 카드 정비로 이어집니다. 자세한 규칙은 `Docs/TetrominoUpgrades.md`를 참고하세요.

- 5개 스테이지, 기본 스테이지당 8핸드. HIT / STAND 및 Ace 1·11·자동 선택.
- 2~21은 처음부터 안전하며, 22~33은 열쇠 블록으로 개별 해금합니다.
- 딜러 정비: 12장 중 5장 공개, 2장 무료 선택. 선택 즉시 효과 적용.
- 추가 공개 비용 1·2·3…코인, 추가 선택 비용 2·3·4…코인. 두 가격은 독립적으로 증가합니다.
- 미선택 카드는 정비 종료 시 소멸. 다음 정비에서 무료 횟수와 가격이 초기화됩니다.

## 소스와 검증

- `Assets/Scripts/TableRules.cs`: 플레이 및 점수 규칙
- `Assets/Scripts/DealerRules.cs`: 딜러 후보 생성과 선택 비용
- `Assets/Scripts/StampRules.cs`, `StampView.cs`: 블록 강화 규칙과 UI
- `Assets/Scripts/TableView.cs`, `DealerView.cs`: 화면
- `Assets/Resources/TableBalance.json`: 밸런스 수치
- Unity 메뉴 `Tools > Number Table > Check Dealer`: 딜러 규칙 검사
- `Assets/Editor/TableProjectSetup.cs`의 `RunCoreChecks()`: 핵심 규칙 검사

최근 검증: 딜러 138개, 핵심 89개 통과. 이전 상점 기반 시뮬레이션과 Historical 0.2 Shop Balance는 과거 참고용이며 현재 딜러의 승률을 나타내지 않습니다.

## MCP 개발 환경

`Packages/com.coplaydev.unity-mcp`에 MCP for Unity 10.2.0 에디터 패키지를 포함했습니다. 서버 런타임 및 AI 클라이언트 설정은 각 컴퓨터에서 별도로 준비하세요. 패키지 README와 Unity MCP 설정 창을 참고하세요. MCP 연결 없이도 게임을 열고 실행할 수 있습니다.

원래 개발 컴퓨터의 상대 경로에 의존하는 `UnityMcpAutoConnect.cs`는 버전 관리에서 제외했습니다. Python/uv 설치, API 키, 로컬 MCP 연결 정보 및 AI 클라이언트 설정은 공유하지 않습니다.

## Git 관리

Assets의 `.meta` 파일, Packages의 manifest/lock과 임베디드 패키지, ProjectSettings를 함께 커밋합니다. Library, Temp, Logs, UserSettings, 빌드 출력, IDE 생성 파일은 자동 재생성되므로 제외합니다. 다른 컴퓨터에서 작업을 시작하기 전 pull하고, 작업 후 변경을 commit/push하세요.

MCP for Unity는 CoplayDev의 MIT 라이선스 소프트웨어입니다. 해당 패키지의 LICENSE를 참고하세요.
