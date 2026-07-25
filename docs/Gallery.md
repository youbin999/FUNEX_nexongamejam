# 갤러리 — 엔딩 영구 보관 & 열람

엔딩 씬에서 만들어진 **크레딧 텍스트 + 이미지 한 쌍**을 로컬에 영구 저장하고, 타이틀에서 다시 열람한다.
기획서의 "갤러리는 영구히 저장되어 나중에도 확인할 수 있음" 요구를 담당한다.

엔딩 생성 파이프라인은 [Ending_LLM.md](Ending_LLM.md) 참고.

## 전체 흐름

```
EndingCreditController (엔딩 씬)
   │  롤 종료 → 에필로그 → 이미지 합류
   ▼
WorldNamePrompt  "당신의 world의 이름을 정해주세요"
   │  이미지 축소본 + 이름 입력 칸 → 엔터 / 확정 버튼
   ▼
GalleryStore.Save(대본, 이미지, RunResult, 이름)
   │  PNG 복사 + index.json 에 엔트리 추가
   ▼
{persistentDataPath}/gallery/
   │
   ▼
GalleryScreen (갤러리 씬)  ← 엔딩 직후 자동 진입 / 타이틀의 갤러리 버튼
   ├─ 격자 목록 (최신순, 스크롤) — 칸마다 이름 + 날짜
   └─ 칸 클릭 → GalleryDetailView (좌: 확대 이미지 / 우: 크레딧 본문)
```

## 저장 구조

```
{Application.persistentDataPath}/gallery/
├── index.json                                ← GalleryDatabase
└── images/
    └── 20260725_213044_9A3F1C4B.png
```

`index.json` 의 `entries` 는 **추가 순서가 곧 시간 순서**다. 정렬 없이 그대로 나열하면 된다.

| 필드 | 설명 |
|---|---|
| `id` | `{저장시각}_{조합키 해시}`. 이미지 파일명이기도 하다 |
| `savedAt` | ISO 8601 (로컬) |
| `worldName` | 플레이어가 붙인 이름(최대 20자). 이 필드가 없던 저장본은 "이름 없는 세계"로 표시된다 |
| `imageFile` | `images/` 안의 파일명 |
| `creditLines` | 크레딧 본문. 엔딩에서 흘러갔던 줄 그대로 |
| `epilogue` | 마무리 한 문장 |
| `combinationKey` | 이 판의 조합 키 |
| `endedEarly` / `endedAtEra` | 핵심 미니게임 실패로 중단된 판인지 |

### 왜 엔딩 이미지 캐시를 그대로 안 쓰는가

`endings/` 캐시는 조합 키로 **덮어써지는** 임시 저장소이고 시간 정보가 없다.
갤러리는 PNG 를 자기 폴더로 복사해 캐시가 지워지거나 덮어써져도 영향받지 않는다.

## 저장 시점

`EndingCreditController` 가 **크레딧 롤 · 에필로그 · 이미지 페이드인이 모두 끝나고,
플레이어가 이름을 확정한 뒤** 저장한다.
이미지는 롤과 병렬로 생성되므로 이름을 묻기 직전에 코루틴을 합류시킨다(`yield return imaging`).
생성기에 `imageTimeout`(기본 60초)이 걸려 있어 무한정 기다리지 않는다.

이름을 물어보는 것이 저장보다 앞이므로, **탭을 띄운 채 게임을 끄면 그 판은 남지 않는다.**
확정을 누르면 저장 후 `gallerySceneName`(기본 `00000_Gallery`) 씬으로 넘어간다.

**이미지가 없으면 저장하지 않는다.** 갤러리는 텍스트와 이미지가 한 쌍일 때만 의미가 있다.
따라서 아래 두 경우에는 갤러리에 아무것도 남지 않는다.

| 상황 | 이유 |
|---|---|
| `forceFallback = true` | 이미지 생성 자체를 건너뛴다 |
| 이미지 생성 실패 (네트워크·안전 필터) | 경고만 남기고 저장 생략 |

이 두 경우에는 **이름 입력 탭도 뜨지 않는다** — 저장되지 않을 이름을 받아봐야 갈 곳이 없다.
크레딧이 끝나면 곧바로 갤러리 씬으로 넘어간다.

같은 조합으로 여러 번 엔딩을 봐도 **매번 새 엔트리로 쌓인다.** 중복 판정은 하지 않는다.

## 파일 구성

| 파일 | 역할 |
|---|---|
| [GalleryEntry.cs](../Assets/01_Scripts/Gallery/GalleryEntry.cs) | 엔트리 데이터 + 인덱스 래퍼 |
| [GalleryStore.cs](../Assets/01_Scripts/Gallery/GalleryStore.cs) | 저장/로드. 모든 IO 는 예외를 삼키고 경고만 남긴다 |
| [GalleryScreen.cs](../Assets/01_Scripts/Gallery/GalleryScreen.cs) | 갤러리 씬 총괄. 목록 생성, 텍스처 수명 관리, ESC/Back |
| [GalleryItemView.cs](../Assets/01_Scripts/Gallery/GalleryItemView.cs) | 목록의 칸 하나 (썸네일 + 이름 + 날짜) |
| [WorldNamePrompt.cs](../Assets/01_Scripts/Ending/WorldNamePrompt.cs) | 엔딩 끝의 이름 입력 탭 |
| [Editor/NamePromptBuilder.cs](../Assets/01_Scripts/Ending/Editor/NamePromptBuilder.cs) | 이름 입력 탭을 엔딩 씬에 세워주는 에디터 도구 |
| [GalleryDetailView.cs](../Assets/01_Scripts/Gallery/GalleryDetailView.cs) | 상세 패널 |
| [Editor/GallerySceneBuilder.cs](../Assets/01_Scripts/Gallery/Editor/GallerySceneBuilder.cs) | 갤러리 UI 를 한 번에 세워주는 에디터 도구 |
| [TitleMenu.cs](../Assets/01_Scripts/Title/TitleMenu.cs) | 타이틀의 `Gallery()` — 갤러리 씬 진입 |

## 씬 구성

### 1. 갤러리 씬

`Assets/00_Scenes/00000_Gallery.unity` 를 열고 **[Tools > Gallery > 갤러리 씬 구성]** 실행.
아래 구조가 통째로 생성되고 참조까지 연결된다. 마음에 안 들면 Ctrl+Z 한 번으로 전부 취소된다.

```
Gallery Canvas [GalleryScreen]
├── Background
├── Title                        "GALLERY"
├── Scroll View [ScrollRect]
│   ├── Viewport [RectMask2D]
│   │   └── Content [GridLayoutGroup + ContentSizeFitter]   ← 칸이 생성되는 곳
│   └── Scrollbar Vertical
├── Empty Message                저장본이 없을 때만 표시
├── Detail Panel [GalleryDetailView]   (비활성으로 시작)
│   ├── Image [RawImage + AspectRatioFitter]     화면 왼쪽
│   ├── Credit Scroll [ScrollRect]               화면 오른쪽
│   │   └── Viewport/Content → Name / Date / Credit / Epilogue
│   └── Close Button
└── Back Button

EventSystem [InputSystemUIInputModule]
```

아이템 프리팹은 `Assets/03_Prefabs/Gallery/GalleryItem.prefab` 에 만들어진다.
이미 있으면 새로 만들지 않고 그것을 쓴다 — 프리팹을 꾸며둔 뒤 다시 실행해도 덮어쓰지 않는다.

> **폰트 주의.** 생성되는 TMP 텍스트는 기본 폰트를 쓴다. 한글이 네모로 깨지므로
> 엔딩 씬과 같은 `KMU80TTFSungkokSerif SDF` 로 바꿔야 한다.

`GalleryScreen` 인스펙터 옵션:

| 필드 | 기본값 | 설명 |
|---|---|---|
| `newestFirst` | **true** | 엔딩 직후 방금 만든 world 가 맨 앞에 오도록 켜 두었다 |
| `titleSceneName` | `00000_Title` | Back 으로 돌아갈 씬 |

칸 하나는 **썸네일 / 이름(26pt) / 날짜(18pt)** 세 줄로 구성된다. `nameLabel` 을 비워두면 이름은
표시되지 않고, 상세 패널은 `nameText` 가 없으면 이름을 날짜 줄 위에 붙여 보여준다.

### 2. 이름 입력 탭 (엔딩 씬)

`Assets/00_Scenes/99_Ending.unity` 를 열고 **[Tools > Ending > 이름 입력 탭 구성]** 실행.
`EndingCreditController` 의 `namePrompt` 까지 자동으로 연결된다.

```
Name Prompt [Canvas(Overlay, sortingOrder 100) + WorldNamePrompt]   ← 항상 활성
└── Panel                                (비활성으로 시작 = panelRoot)
    ├── (Dim)                            전면을 덮어 뒤쪽 클릭을 막는다
    └── Tab
        ├── Title                        "당신의 world의 이름을 정해주세요"
        ├── Preview Area/Preview [RawImage + AspectRatioFitter]
        ├── Name Field [TMP_InputField]  최대 20자, 엔터로 확정
        └── Confirm Button               "확정" (이름이 비면 눌리지 않는다)
```

전용 오버레이 캔버스를 쓰는 이유는 크레딧이 월드 스페이스 캔버스(`CrawlCanvas`)에서 굴러가기
때문이다. 기존 캔버스에 얹으면 크레딧 뒤로 깔린다.

> **WorldNamePrompt 는 항상 활성인 오브젝트에 붙이고 `panelRoot` 에 탭을 연결한다.**
> 자기 자신을 껐다 켜면 리스너를 묶는 `Awake` 가 다시 돌면서 패널을 도로 닫는다.

확정 버튼의 `OnClick` 은 `WorldNamePrompt.Awake` 가 코드로 묶으므로 인스펙터에는 비어 있다.

### 3. 타이틀 진입 버튼

타이틀 버튼은 Canvas 아래의 평범한 UGUI `Button` 이고, 동작은 `Title_Menu` 오브젝트의
[TitleMenu](../Assets/01_Scripts/Title/TitleMenu.cs) 가 담당한다. `NEW_WORLD` 버튼과 같은 방식으로 연결한다.

1. Hierarchy 에서 `NEW_WORLD` 버튼을 복제(Ctrl+D)해 `GALLERY` 로 이름 변경
2. 자식 텍스트를 `GALLERY` 로 바꾸고 위치를 조정
3. `Button` 의 `On Click ()` 에서
   - 기존 항목의 대상은 이미 `Title_Menu` 로 들어가 있다
   - 함수만 `TitleMenu > NewWorld ()` 에서 **`TitleMenu > Gallery ()`** 로 변경

`gallerySceneName` 기본값은 `00000_Gallery` 이며 `Title_Menu` 인스펙터에서 바꿀 수 있다.

### 4. 빌드 설정

`00000_Gallery` 씬은 **[Tools > Gallery > 갤러리 씬 구성] 실행 시 자동으로 빌드 설정에 추가된다**
(씬이 저장된 상태여야 한다). 수동으로 넣으려면 File > Build Profiles 에서 추가.

엔딩이 끝나면 씬을 직접 넘기므로 `99_Ending` 과 `00000_Gallery` 가 **둘 다** 등록돼 있어야 한다.

## 메모리

`GalleryScreen` 은 씬에 들어올 때 모든 엔트리의 PNG 를 `Texture2D` 로 읽어 목록과 상세가 **공유**하고,
`OnDestroy` 에서 한꺼번에 해제한다. 장당 1280×720 RGBA ≈ 3.5MB 이므로 수십 장까지는 문제없다.
수백 장 규모가 되면 화면에 보이는 칸만 로드하는 방식으로 바꿔야 한다.

## 테스트

1. `EndingCreditController` 의 `forceFallback` 을 **끈 채로** 엔딩을 완주한다(폴백 모드로는 저장되지 않는다).
2. 크레딧이 끝나면 이름 입력 탭이 뜨는지 확인한다.
   - 한글 이름이 네모로 깨지지 않는지
   - 빈 칸에서는 확정 버튼이 회색인지
   - 엔터와 확정 버튼 **양쪽 다** 통하는지, 그리고 곧바로 갤러리 씬으로 넘어가는지
3. `%USERPROFILE%\AppData\LocalLow\<회사명>\<제품명>\gallery\` 에 `index.json` 과 PNG 가 생겼는지,
   `worldName` 에 입력한 이름이 들어갔는지 확인.
4. 에디터를 재시작하고 타이틀 → 갤러리 → 칸 클릭 → 상세 → ESC 순으로 확인.
5. 서로 다른 판으로 2~3회 반복해 최신순으로 쌓이는지, 그림이 매번 다른지 확인.
6. 이 기능 이전에 저장된 엔트리가 있다면 "이름 없는 세계"로 뜨는지 확인.

저장본을 비우려면 `gallery` 폴더를 통째로 지우면 된다.
