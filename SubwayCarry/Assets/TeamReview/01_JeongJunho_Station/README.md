# 정준호 검토용 복사본

이 공유본은 버티컬 슬라이스의 맵/아트 의존 자료로도 사용한다. 실행 씬은 `Assets/_Project/Scenes/Prototype/ArtMap_GameplaySlice/ArtMap_01_Gachon_Train_Wangsimni_Playable.unity`다. 아래 원본 기준 씬과 아트는 보존하며 통합용 배치/코드는 별도 폴더에 있다.

원격 브랜치: `feature-station`
기준 커밋: `adc631e6d4cf359bc0f6f7fa3742b17093350af8`

## 열어볼 씬

- `Scenes/GachonUniv.unity`
- `Scenes/Subway.unity`
- `Scenes/Wangsimni.unity`

기존 _Project와 통합하지 않은 팀원 원본 기준의 독립 검토본입니다. GUID와 SubwayCarry 네임스페이스 및 하드코딩된 에셋 경로만 이 폴더 전용으로 변경했습니다. 동작 로직은 변경하지 않았습니다. 씬에서 참조하는 에셋과 C# 타입 의존성을 함께 포함하며, 원본의 에디터 생성 도구와 Tests, Library, Temp, Logs는 가져오지 않았습니다.

다른 팀원 씬과 동시에 Additive 로드하지 말고 씬 하나씩 열어 확인하세요. 시각/플레이 검증은 사용자가 수행합니다. IMPORT_MANIFEST.json에 파일별 출처와 GUID 대응을 기록했습니다.
