# Cookie Clicker (Unity 6)

Unity 6 (6000.3, URP)로 만든 쿠키 클리커 클론입니다. 가운데 쿠키는 3D 모델(`Cookie.glb`)이고, 클릭하면 쿠키를 얻고 건물과 업그레이드를 사서 자동 생산량을 늘립니다.

## 처음 여는 방법

1. Unity Hub에서 이 폴더를 엽니다. `glTFast` 패키지가 자동으로 설치되고 `Cookie.glb`가 임포트됩니다 (모델이 커서 처음엔 1~2분 걸릴 수 있습니다).
2. 상단 메뉴에서 **Tools > Cookie Clicker > Build Scene (씬 만들기)** 를 한 번 누릅니다.
3. `Assets/CookieClicker/Scenes/CookieClicker.unity`가 열리면 ▶ Play를 누릅니다.

씬 만들기는 처음 한 번만 하면 됩니다. 그 뒤로는 일반 씬처럼 Hierarchy와 Inspector에서 직접 편집하면 됩니다.
다시 실행하면 씬과 프리팹은 새로 만들지만, 건물·업그레이드 데이터 에셋은 덮어쓰지 않습니다.

## 기능

- 3D 쿠키 클릭 (누를 때 찌그러짐, 마우스를 따라 기울어짐, 부스러기와 +N 글자 연출)
- 건물 12종, 1개/10개/100개 단위 구매, 가격은 하나 살 때마다 1.15배
- 업그레이드 25종 (건물 2배, 클릭 2배, 클릭할 때 초당 생산량의 1% 추가)
- 황금 쿠키: 럭키(쿠키 즉시 획득) 또는 프렌지(77초 동안 생산량 7배)
- 쿠키 비, 회전하는 후광, 뉴스 문구, 통계
- 30초마다 자동 저장, 종료할 때도 저장, 초기화(두 번 눌러야 실행)

## 폴더 구조

```
Assets/CookieClicker/
├─ Data/          건물·업그레이드·GameConfig 에셋 (밸런스 수치는 여기서 수정)
├─ Models/        Cookie.glb
├─ Prefabs/       BuildingRow, UpgradeRow, FloatingText, Crumb, RainCookie
├─ Scenes/        CookieClicker.unity
├─ Art/Generated/ UI 스프라이트 (같은 이름의 PNG로 덮어쓰면 교체됨)
└─ Scripts/
   ├─ Runtime/    (CookieClicker.asmdef)
   │  ├─ Data/      ScriptableObject 정의
   │  ├─ Core/      게임 규칙, 상태, 계산
   │  ├─ Save/      저장/불러오기
   │  ├─ Gameplay/  3D 쿠키
   │  ├─ View/      UI 표시
   │  └─ FX/        이펙트
   └─ Editor/     (CookieClicker.Editor.asmdef) 씬·프리팹·데이터 생성 도구
```

## 설계 원칙

```
 데이터(ScriptableObject)  →  CookieGame(규칙)  ──이벤트──▶  View / FX (화면)
                                   ▲
                       화면은 CookieGame의 public 함수만 호출
```

- **데이터와 코드를 분리**: 가격·생산량은 에셋에 있으므로 기획 수정에 코드 변경이 필요 없습니다.
- **로직은 화면을 모름**: `CookieGame`은 UI 클래스를 하나도 참조하지 않고 이벤트만 발생시킵니다. UI를 통째로 바꿔도 로직은 그대로입니다.
- **`[SerializeField] private`**: 인스펙터에서는 연결할 수 있지만 다른 스크립트가 마음대로 값을 바꾸지 못하게 막습니다.
- **Assembly Definition**: 런타임/에디터 코드를 분리해서 컴파일 시간이 줄고, 에디터 코드가 빌드에 섞이지 않습니다.

## 스크립트 설명

### Data

**`BuildingDefinition`** / **`UpgradeDefinition`**: 건물·업그레이드 하나의 고정 데이터(이름, 가격, 효과, 해금 조건).
- `Id`: 세이브 파일에는 배열 순서가 아니라 이 ID가 저장됩니다. 나중에 건물 순서를 바꾸거나 중간에 새 건물을 끼워 넣어도 기존 세이브가 깨지지 않습니다.
- `UpgradeDefinition.EffectType`: 효과 종류를 enum으로 나눴습니다. 새 업그레이드는 에셋만 추가하면 되고, 새로운 *종류*의 효과가 필요할 때만 `ProductionCalculator`에 case 하나를 추가하면 됩니다.

**`GameConfig`**: 건물·업그레이드 목록과 밸런스 수치(가격 증가율, 황금 쿠키 주기, 자동 저장 간격 등)를 한곳에 모은 에셋.
- 수치가 코드 여기저기에 흩어지지 않으므로 기획자가 Inspector 하나만 보고 밸런스를 조정할 수 있습니다.

### Core

**`CookieGame`**: 게임 규칙의 중심. `GameManager` 오브젝트에 붙어 있습니다.
- `TryBuyBuilding(int)` / `TryBuyUpgrade(int)`: 가격 확인 → 차감 → 생산량 재계산 → `BuildingPurchased` 이벤트 순서로 처리합니다. 구매 규칙이 이 함수 하나에만 있어서 상점 UI가 여러 개가 되어도 규칙이 어긋나지 않습니다.
- 이벤트(`CookieClicked`, `GoldenCookieClaimed` 등): 결과를 직접 UI에 그리지 않고 알리기만 합니다. 효과음이나 업적 시스템을 추가할 때도 이벤트를 구독하는 스크립트만 새로 만들면 되고 `CookieGame`은 고칠 필요가 없습니다.

**`GameState`**: 보유 쿠키, 건물 개수처럼 플레이 중에 변하는 값만 모은 순수 데이터 클래스.
- `CreateNew(config)`: 설정(변하지 않는 값)과 상태(변하는 값)를 분리해 두어서 저장할 때 상태만 직렬화하면 되고, 초기화도 새 객체 하나 만드는 걸로 끝납니다.

**`ProductionCalculator`**: 생산량 계산만 하는 static 클래스.
- `Calculate(config, state)`: 입력만으로 결과가 정해지는 순수 함수라 씬 없이 단위 테스트를 만들 수 있습니다. 매 프레임이 아니라 구매할 때만 호출해서 결과를 캐시합니다.
- `BulkCost(...)`: 10개, 100개 구매 가격을 반복문 대신 등비수열 공식으로 계산합니다.

**`NumberFormatter`**: `1,234.5만`, `3.2억`처럼 한국식 단위로 표시합니다.
- 모든 UI가 이 함수 하나를 쓰므로 표기 방식을 바꾸고 싶으면 여기만 고치면 됩니다.

### Save

**`SaveSystem`** / **`SaveData`**: `GameState`를 JSON으로 바꿔 PlayerPrefs에 저장합니다.
- `Save` / `TryLoad`: 저장 형식(`SaveData`)과 게임 상태(`GameState`)를 따로 둬서, 나중에 파일 저장이나 클라우드 저장으로 바꿀 때 이 파일만 수정하면 됩니다. `version` 필드가 있어서 형식이 바뀌어도 구버전 세이브를 변환할 수 있습니다.
- 불러오기에 실패하면 예외로 게임이 멈추지 않고 경고만 남긴 뒤 새 게임으로 시작합니다.

### Gameplay

**`CookieClickTarget`**: 씬의 3D 쿠키(`Cookie` 오브젝트).
- `OnPointerDown`: 카메라의 `PhysicsRaycaster`가 쿠키의 `SphereCollider`를 맞히면 EventSystem이 호출합니다. UI 버튼과 같은 입력 경로를 쓰기 때문에 UI 위를 클릭했을 때 뒤의 쿠키가 같이 눌리는 문제가 자동으로 막힙니다.
- `FitToScreen`: 해상도가 바뀌어도 쿠키가 화면 왼쪽에 같은 비율로 보이게 합니다. 직접 배치하고 싶으면 Inspector에서 `Fit To Screen`을 끄면 됩니다.

### View

**`StoreView`**: 오른쪽 상점 패널.
- `Refresh`: 씬에 미리 배치된 줄(Row)에 데이터를 채우고 공개/숨김을 정합니다. 공개된 건물 다음 하나는 `???`로 보여 줘서 다음 목표를 제시합니다.
- 0.1초마다만 갱신하고, 구매 이벤트가 오면 즉시 갱신합니다. 매 프레임 30여 줄의 텍스트를 다시 그리지 않아도 됩니다.

**`BuildingRowView`** / **`UpgradeRowView`**: 상점 한 줄. 프리팹 하나로 모든 줄을 만듭니다.
- `Bind(index, callback)`: 클릭 이벤트를 코드에서 연결합니다. Inspector의 OnClick에 함수 이름을 문자열로 걸어 두면 함수 이름을 바꿨을 때 조용히 끊어지는데, 코드로 연결하면 컴파일 단계에서 오류로 잡힙니다.
- `ShowBuilding` / `ShowMystery`: 줄은 "받은 값을 그리기만" 하고 계산은 하지 않습니다. 디자인을 바꾸려면 프리팹만 수정하면 모든 줄에 반영됩니다.

**`HudView`**: 쿠키 수, 초당 생산량, 프렌지 표시.
- 표시할 문자열이 바뀌었을 때만 `Text`에 대입합니다. UI는 텍스트가 바뀌면 메시를 다시 만들기 때문에 불필요한 갱신을 줄입니다.

**`StatsView`**: 가운데 통계와 건물별 생산 비중.
- `StringBuilder`를 재사용해서 0.5초마다 갱신할 때 생기는 메모리 할당(GC)을 줄입니다.

**`ClickFeedbackView`**: 클릭 +N 글자, 부스러기, 황금 쿠키 안내 문구.
- `OnCookieClicked`: `CookieClickTarget.Clicked` 이벤트를 받아서 연출만 합니다. 연출을 끄고 싶으면 이 컴포넌트를 비활성화하면 되고 게임 로직은 영향을 받지 않습니다.

**`GoldenCookieView`**: 황금 쿠키 등장·클릭.
- `Show` / `Hide`: 오브젝트를 매번 생성·삭제하지 않고 `CanvasGroup`으로 보이기만 바꿉니다. 씬에 항상 있으므로 Inspector에서 크기나 이미지를 바로 편집할 수 있습니다.

**`NewsTickerView`**: 상단 뉴스 한 줄. 기본 문구는 Inspector 배열에서 추가·수정할 수 있습니다.

**`MenuView`**: 저장 / 초기화 버튼. 초기화는 3초 안에 두 번 눌러야 실행돼서 실수로 진행 상황을 날리지 않습니다.

### FX

**`UIParticle`**: 날아가다 사라지는 UI 조각 하나.
- `Launch(...)`: 속도·중력·회전·수명만 다르게 주면 +N 글자, 부스러기, 쿠키 비를 모두 이 스크립트 하나로 만듭니다. 비슷한 스크립트를 여러 개 만들지 않아도 됩니다.

**`CookieRain`**: 초당 생산량에 비례해서 배경에 쿠키를 떨어뜨립니다. 로그 스케일이라 생산량이 엄청 커져도 화면이 쿠키로 덮이지 않습니다.

**`Spinner`**: 쿠키 뒤 후광 회전.

### Editor

**`CookieSceneBuilder`**: `Tools > Cookie Clicker > Build Scene` 메뉴.
- `BuildScene`: 씬·프리팹을 **파일로** 저장합니다. 실행할 때 오브젝트를 만드는 방식이 아니어서 플레이하지 않아도 모든 오브젝트가 Hierarchy에 보이고 직접 편집할 수 있습니다.
- `Wire(...)`: `[SerializeField] private` 필드를 `SerializedObject`로 연결합니다. Inspector에서 드래그해서 연결하는 것과 같은 결과입니다.

**`CookieContentFactory`**: 기본 건물·업그레이드 에셋 생성. 이미 있는 에셋은 덮어쓰지 않아서 직접 조정한 수치가 지워지지 않습니다.

**`CookieArtFactory`**: UI용 스프라이트(둥근 사각형, 후광, 2D 쿠키)를 PNG로 생성합니다. 디자이너가 만든 이미지를 같은 경로에 덮어쓰면 그대로 교체됩니다.

## 자주 하는 수정

| 하고 싶은 것 | 위치 |
|---|---|
| 건물 가격·생산량 변경 | `Data/Buildings/Building_*.asset` |
| 새 건물 추가 | 에셋 생성(우클릭 > Create > Cookie Clicker > Building) → `GameConfig`의 Buildings에 추가 → `StorePanel`에 `BuildingRow` 프리팹을 하나 더 넣고 `StoreView`의 Building Rows에 연결 |
| 황금 쿠키 빈도 | `Data/GameConfig.asset` |
| 쿠키 위치·크기 | `Cookie` 오브젝트의 `CookieClickTarget` |
| 쿠키 모델 방향 | `Cookie/ModelPivot`의 Rotation |
| 상점 줄 디자인 | `Prefabs/BuildingRow.prefab` |
