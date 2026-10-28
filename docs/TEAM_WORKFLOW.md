# 팀 원본 + 통합 테스트 작업 방식

## 원칙

2026-10-11부터 팀 공유 기준은 `main`, 우선 개발 대상은 3D 1인칭이다. `develop`과 역할 브랜치는 기존 이력을 보존하고 최신 `origin/main`을 기준으로 작업한다.

각 팀원이 올린 원본 폴더, 씬 이름, 프리팹과 GUID를 그대로 채용한다. 통합 담당자가 팀원 구현을 별도 폴더로 복제하거나 `TeamReview` 네임스페이스로 다시 만들지 않는다. 원본을 참조하는 통합 씬과 연결 코드만 별도로 관리한다.

| 구분 | 담당 | 기준 브랜치 | 기존 원본/작업 위치 |
| --- | --- | --- | --- |
| 플레이어·운반·균형·파손 | 김준 | `role/player-gameplay` | `Assets/_Project/Scripts/Gameplay`, `Prefabs/Gameplay`, `Art/Concepts/Characters`, `Scenes/Prototype_Core.unity` |
| 역·객차 아트 및 맵 | 정준호 | `feature-station` | `Assets/_Project/Art/Sprites`, `Scenes/GachonUniv.unity`, `Scenes/Subway.unity`, `Scenes/Wangsimni.unity`, 해당 문 프리팹 |
| 배송·경제·강화·UI | 조재형 | `role/delivery-economy`, 기존 `stage-economy-test` | `Assets/_Project/Scripts/Delivery`, `Data/Delivery`, 관련 UI 및 기존 프로토타입 코드 |
| 승객 AI와 회귀 테스트 | 신찬욱·조재형 | `role/passenger-ai` | `Assets/_Project/Scenes/Ai_v2`, `Scripts/AI` |
| 전체 연결과 통합 테스트 | 신찬욱 | `main` 통합 | 3D: `Scenes/Gameplay3D`, 기존 2D: `Scenes/Prototype/ArtMap_GameplaySlice` |
| 공통 계약 | 관련 담당자 공동 | `main` 기준 합의 | `Assets/_Project/Scripts/Core/Contracts` |

위 표는 소유 범위를 설명한다. 파일을 이 표에 맞춰 새 경로로 옮기라는 지시가 아니다. 경로 앞의 `Assets`는 저장소의 `SubwayCarry/Assets`이다.

## 통합 폴더

우선 개발 폴더는 `Assets/_Project/Scenes/Gameplay3D`, 진입 씬은 `SubwayCarry_3D_FirstPerson_Delivery.unity`다. 코드·데이터·프리팹·아트·Blender 원본은 기존 하위 경로에서 수정한다.

팀원 3D 맵 원본은 `Scenes/GachonUniv_3D.unity`, `Subway_3D.unity`, `Wangsimni_3D.unity`와 `Art/StationsSprite3D`, `Art/SubwayFirstPerson`, `Scripts/Transit`에 유지한다. 시설·충돌·이동 정보 연결과 실제 실행 검증은 별도 작업이다.

2D 씬·스프라이트·프리팹·GUID·원래 경로와 공용 2D 렌더 설정·시작 씬을 보존한다. 통합을 이유로 `_dump`로 일괄 이동하지 않는다.

아래는 보존하는 2D `ArtMap_GameplaySlice`의 기존 사용 기준이다. 이름을 바꾸거나 새 버전 폴더로 복제하지 않는다.

- 진입 씬: `ArtMap_01_Gachon_Train_Wangsimni_Playable.unity`
- `Scripts`: 원본 플레이어와 AI를 연결하는 어댑터, 이동 경계, 맵 전환 및 상황 제공 코드
- `Player_Slice.prefab`: 통합용 속도·윤곽·크기 등 테스트 설정. 스크립트와 이미지 원본은 공용 자산을 참조
- 통합 씬의 맵 배치는 테스트용 인스턴스/데이터다. 팀원 원본 씬을 덮어쓰거나 자동 재생성하지 않는다.
- 소스 맵 갱신은 자동으로 기존 통합 배치에 전파되는 것이 아니다. 통합 담당자가 변경을 비교하고 통합 씬에 반영·검증한다.
- `Apply Player Sitting Sprites` 통합 메뉴는 공용 이미지/프리팹을 읽고 통합 프리팹만 갱신한다. 원본 이미지 임포트와 원본 프리팹 생성은 플레이어 담당 도구의 역할이다.

## 반복 작업 순서

1. 팀원은 fetch 후 최신 `origin/main`을 확인한다. 로컬 수정·커밋을 보존하며 역할 브랜치에 병합하고, 갈라진 이력을 임의 rebase·강제 pull하지 않는다.
2. 자신이 담당하는 원본 폴더의 기능을 수정하고 담당 씬에서 시험한다.
3. 해당 파일과 필요한 `.meta`만 커밋하여 자기 브랜치에 올린다. 다른 담당자의 파일, 통합 씬, 개인 설정을 섞지 않는다.
4. 통합 담당자가 변경 내용을 확인하고 `main`에 병합한다. 연결 수정은 해당 3D/2D 통합 폴더에서 처리한다.
5. 공통 인터페이스 변경이 필요하면 생산자·소비자를 함께 확인하고 관련 담당자와 맞춘다. 각자 다른 계약 복사본을 만들지 않는다.
6. 정적 참조·컴파일 검사와 사용자 플레이 확인을 구분한다. 문제를 팀원 원본으로 되돌려 고쳐야 한다면 담당자에게 파일과 증상을 전달한다.

`main` 직접 작업도 가능하나 최신 원격·로컬 변경을 먼저 확인한다. 같은 씬·공유 파일의 동시 편집은 담당자를 정하고, 요청받은 경로만 Push한다.

`Scenes/Prototype/GameplayFlow_Prototype.unity`처럼 여러 역할이 이미 함께 사용하는 파일은 각자 독립 소유 파일로 취급하지 않는다. 편집 전에 담당자를 정하고, 변경이 겹치면 통합 담당자가 3-way로 병합한다. 이 예외를 해결하려고 원본을 역할별 복사본으로 늘리지 않는다.

## 모션 제작·검수

Unity 프로젝트는 저장소의 `SubwayCarry`다. 아래 경로의 기준은 `SubwayCarry/Assets/_Project/Scenes/3D/Test/Animation`이다.

| 용도 | 경로 |
| --- | --- |
| 전신 FK 동작 제작 | `MotionAuthoring.unity` |
| 기본 이동·전환 검수 | `Normal.unity` |
| 주변 상황·시설 접점 검수 | `Situations.unity` |
| 다시 편집할 원본 | `Authored/<제작자>_<동작명>.asset` |
| 재생·연동용 출력 | `Exports/<제작자>_<동작명>.anim`, `.fbx` |
| 제작 전용 캐릭터 | `Prefabs/MotionRig.prefab` — 기존 더미와 같은 골격·외형·스케일, 게임/보행 실험 컴포넌트 제외 |

- `MotionAuthoring`은 Play 없이 Scene 뷰에서 편집한다. 포즈·키·초안은 **F2로 `.asset`에 저장**하며, 씬 저장만으로 저장되지 않는다. 공통 씬을 사람별로 복제하지 않는다. 환경 배치를 바꿀 때만 씬 변경 담당자를 정한다.
- `Normal`·`Situations`의 폰 보기 대표 시험은 `Data/Resources/PassengerPhoneReview.asset`으로 선택한다. `normalEnabled`는 1명 동작 검수, `situationsEnabled`는 1명+교행 승객 검수다. 각각 끄면 기존 보행 검수로 돌아간다. 씬 파일·기존 캐릭터·인게임 기본 모션은 교체하지 않는다.
- **Situations 한 씬**의 오른쪽 위 `동작 검수`에 마우스를 올리면 동작·편집 원본 메뉴가 열린다. `1 자동 / 2 주머니 / 3 한 손 보기 / 4 양손 가로 / 5 들고 걷기 / 6 사람 사이 비집고 지나가기`도 유지한다. 메뉴 밖에서 우클릭 드래그로 회전, 휠로 확대, PageUp/PageDown으로 관찰 높이를 조절한다. 수동 폰 검수는 같은 승객의 실제 왕복 보행이며 목적지 도달 시 순간이동시키지 않는다. 자동 모드는 기존 목적 이동·교행이다. `편집 원본`에서 선택한 뒤 Play를 끄고 해당 원본을 더블클릭한다.
- 기존 사용자 폰 보기 자세는 보존한다. `Authored/AI_폰_주머니에서꺼내기`, `주머니에넣기`, `올려보기`, `내려들기`, `양손가로잡기`, `양손풀기`, `양손가로유지`, `들고걷기`의 실제 프레임별 원본과 Exports 출력을 사용한다. 양손 동작은 양팔, 나머지는 오른팔 마스크다. 들고 걷기는 공통 CC0 Walk와의 차이를 적용해 기존 보행의 팔 흔들림과 정지 전환을 유지한다. 기존 V2 Navigation/Agent·CharacterController/Motor가 이동·충돌을 소유하며 새 학습 모델·전신 모션 매칭 결과와 구분한다.
- 더미의 주머니는 검수용 허리 파우치다. 꺼내기/넣기의 접촉 프레임에는 제한된 Generic 접점 보정을 하고 폰을 손↔주머니로 전달한다. 양손 유지에는 왼손 지지를 보정한다. 타이밍·도달 위치를 편집할 때 `PassengerPhoneReview.actions`의 접촉·구간 기준 프레임도 확인한다. 접점이 맞지 않으면 폰을 순간이동시키지 않고 확인 필요 상태로 남긴다.
- 사람 사이 통과는 같은 씬에 방향과 간격이 다른 승객 6명을 배치하는 대표 시험이다. 사람 위치는 기존 중앙 CrowdManager에서 조회하고 틈이 부족하면 대기한다. 팔을 모아 어깨부터 진입 → 지지발을 번갈아 옮기는 옆걸음 → 군중을 벗어난 뒤 정면 보행으로 연결한다. 통과 중 다른 메뉴를 선택하면 빠져나온 뒤 전환한다. `Authored/AI_사람사이_왼어깨·오른어깨_진입·옆걸음유지·빠져나오기` 6개 실제 원본과 `PassengerPhoneReview.squeeze`의 같은 제작 ID를 연결한다. ROOT 궤적은 원본/FBX에 보존하고 실행 위치·방향은 기존 CharacterController/Motor가 소유한다. Generic 지지발 보정은 클립 전환 뒤 복원하며, 사용자 자연스러움·접지 승인과 전체 인게임 적용은 별도다.
- 공통 예제 `Authored/WeightShift_Example.asset`은 기존 경로·GUID를 유지한다. 예제를 수정해 새 동작을 만들 때는 제작 창의 **내 사본 만들기**를 먼저 누른다.

제작 순서:

1. `MotionAuthoring.unity`를 열고 `SubwayCarry > Animation > 동작 제작 · MotionAuthoring` 선택.
2. F1로 새 동작 시작. 예제·팀원 동작을 바탕으로 만들 때는 해당 `.asset`을 열고 **내 사본 만들기** 선택.
3. 제작자에 팀 내 고유 이름/별칭, 클립 이름에 동작명 입력. 예: 제작자 `Chanuk`, 동작명 `StandShift` → `Authored/Chanuk_StandShift.asset`. 새 원본은 제작자 없이 저장할 수 없다.
4. Scene 뷰에서 관절 선택. **W 위치 드래그 / E 회전** 또는 창 위쪽 도구 버튼 사용. **오른쪽 마우스를 누른 카메라 이동 중에는 WASD·E·Space를 카메라에 전달**하며 도구·재생 단축키를 실행하지 않는다.
5. 손 하나를 끌면 팔꿈치와 어깨가 함께 반응하고, 팔만으로 부족한 도달 거리는 쇄골·윗몸통에 분산한다. 손 방향은 팔을 따라오며 발은 기존 방향을 유지한다. 팔꿈치·무릎은 손발 위치를 유지하며 굽힘 방향을 조절한다. 어깨 버튼은 쇄골 끝의 실제 어깨 위치를 제어하고, 골반·발을 움직이지 않은 채 윗몸통과 연동한다. 공통 더미의 기준 포즈를 바탕으로 swing/twist·접힘 범위를 제한하며 기존 FK 자세를 갑자기 초기화하지 않는다. 개별 체형별 가동범위와 몸 표면 충돌은 별도 검수다.
6. 전체 이동은 몸통 가운데의 빨간 점, 골반은 별도 위치 핸들이다. 골반 이동 시 발 고정을 양발/왼발/오른발/해제로 선택한다. 손발·팔꿈치·무릎·어깨 이외의 관절은 회전 편집이다. 위치 편집 결과도 본 길이·스케일을 바꾸지 않고 기존 FK 키로 저장한다.
7. F3은 현재 키 저장 후 다음 프레임, 좌우 화살표는 초안을 보존하며 프레임 이동. Space는 저장된 키의 미리보기. 기본 30FPS·60프레임은 2초이며, 기본 설정은 그대로 사용하고 저장할 때 클립 이름·제작자를 입력하면 된다.
8. F2로 원본·Unity 클립·FBX 저장. 임시 출력의 왕복 검증 후 게시하며, 실패 시 이전 정상 파일과 현재 초안을 유지한다. 미확정 초안은 `.asset`에만 남고 `.anim`·FBX에는 F3으로 확정한 키만 포함된다. 연결된 제작 ID의 동작 역할만 갱신해 다음 Normal/Situations Play에 적용한다. 제작창에서도 폰·주머니를 함께 미리 볼 수 있다. 다른 제작물이나 전체 인게임에 자동 적용하지 않는다.
9. 창 상단 **AI 제작 동작** 폴더 버튼은 Project의 Exports, **편집 원본**은 Authored를 연다. 이 도구가 만든 `.asset`·`.anim`·FBX 더블클릭은 제작 ID와 명시 참조로 편집 원본을 찾는다. 외부 FBX는 가로채지 않는다. 새 동작은 **내 사본 만들기**, 담당 동작 수정은 같은 원본에서 F2 갱신한다. 기존 파일명·GUID는 유지된다. 저장 안 한 씬이나 Play 중이면 씬 전환을 보류한다.
10. 지원하는 공통 CC0/직접 제작 Generic 클립은 **선택 클립 전체를 편집 사본으로** 전체 프레임을 가져온다. Humanoid·다른 골격·권한 미확인 클립은 이 경로에서 지원하지 않는다. 반복 클립은 처음/마지막 포즈를 맞추고 반복 옵션을 선택한다.

공유·검수 기준:

- 같은 원본을 동시에 편집하지 않는다. 다른 작업자는 독립 사본을 만든다. Explorer/Project의 단순 복제는 원본 ID·출력 참조를 함께 복제하므로, 저장 시 중복 ID를 차단한다.
- 기본 자세는 기존 Quaternius `UniversalAnimationLibrary/AnimationLibrary_Unity_Standard.fbx`의 CC0 `Idle_Loop`이다. 출처·조건은 같은 폴더의 `License.txt`에 유지한다. 시작 클립은 이 CC0 데이터 또는 제작 도구에서 직접 만든 출력으로 제한한다. 로컬 전용 모션 샘플과 그 파생 데이터는 공유하지 않는다.
- 제작기는 기존 공통 Generic 골격의 실제 본 경로를 사용한다. `FreeWalk` 안의 로컬 전용 Avatar·모션·검수 데이터는 제작 도구의 실행 요건이 아니다. 해당 폴더의 기존 Git 제외 규칙은 유지한다.
- 공통 씬·제작 코드·원본·출력·폴더의 `.meta`, 기존 공통 캐릭터·매핑·CC0 원본 의존성을 함께 공유한다. FBX는 기존 `.gitattributes`의 Git LFS 대상이다. 수신 후 `git lfs pull`로 실제 데이터를 받는다.
- 패키지 기준은 `Packages/manifest.json`·`packages-lock.json`이다. FBX Exporter `5.1.6`, Autodesk FBX `5.1.3`을 사용하며 Unity `6000.3.7f1`의 현재 Generic 골격에서 출력·재임포트를 검증했다. 다른 골격의 리타기팅은 별도 검수다.
- `Logs`, `Library`, `Temp`, `UserSettings`, 개인 복구 파일과 바탕화면 바로가기는 공유 대상이 아니다. 바탕화면은 이 PC의 편의 경로이며 실행 원본은 저장소 안에 둔다.
- 검수자는 원본 열기 → 전체 구간·문제 프레임 확인 → 키 수정 → F2 → 다시 열기 순서로 확인한다. 자연스러움·발 접지는 사용자 시각 검수이며 컴파일·출력 오차 검사와 구분한다.
- 업로드할 때는 이 기능의 정확한 변경 경로와 `.meta`만 선택한다. 미커밋 작업은 아직 팀에 배포된 상태가 아니다. 커밋·푸시는 사용자 요청 범위에서만 진행한다.

## 공통 환경

이번 수신 커밋, 정적 검사 범위 및 원본 담당자 확인 사항은 [INTEGRATION_REVIEW.md](INTEGRATION_REVIEW.md)에 기록한다.

- Unity 버전은 `ProjectSettings/ProjectVersion.txt`, 패키지는 `Packages/manifest.json`과 잠금 파일을 기준으로 한다.
- 폴더를 받기 위해 Unity 버전, 입력/렌더 설정, 시작 씬, Build Settings를 임의로 교체하지 않는다.
- 개인 `SceneTemplateSettings.json`, `_Recovery`, IDE/Library/Temp/빌드 파일은 공유하지 않는다.
- 다른 팀원의 작업 브랜치나 `main`을 강제 갱신·삭제하지 않는다. 이 구조를 받는 팀원도 자신의 기존 수정과 이력을 보존해야 한다.
