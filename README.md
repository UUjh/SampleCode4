# SampleCode4

Unity 클라이언트 포트폴리오 공개용 C# 샘플 코드입니다.
아웃게임 UI 전환(scene/prefab window), 서버 Bootstrap/카탈로그 동기화, Firebase Player API 통신,
requestId 멱등성 기반 구매/뽑기 재시도, Addressables 리소스 관리 구조를 보여줍니다.

실제 서비스 URL, 토큰, 에셋 경로, 세부 정책, 전체 Response 모델, 프로젝트 고유 수치/공식은 제거하거나 `중략` 처리했습니다.
컬렉션, 메일, 일일보상 등 같은 패턴으로 확장되는 콘텐츠 UI는 샘플에서 제외했습니다.

## 폴더 구조

```
Scene/
  PersistentSingleton.cs        // DontDestroyOnLoad 기반 공통 싱글턴
  SampleRuntime.cs              // 단독 씬 테스트용 EventSystem 보정
  SceneLoader.cs                // Single/Additive Scene 로드·언로드 유틸리티
  SceneNames.cs                 // 샘플에서 사용하는 Scene 이름 관리
  Boot/BootFlow.cs              // Firebase 초기화와 첫 씬 결정
  Login/LoginFlow.cs            // 익명 로그인과 유저 문서 보장(/user/ensure)
  Main/MainFlow.cs              // 메인 진입 시 Bootstrap 로드 플로우

Service/
  Addressables/
    AddressableSpriteService.cs // 참조 카운트 기반 Sprite 로드/해제 (SpriteLease)
    AddressablePrefabService.cs // UI window/overlay prefab 생성·해제
  Bootstrap/
    GameBootstrapService.cs     // 유저/상점/가챠/아이템 병렬 로드, 중복 요청 병합, 부분 캐시 갱신
    ShopBootstrapService.cs     // 상점 카탈로그 + 서버 Bootstrap 동기화 (catalogVersion 불일치 복구)
    GachaBootstrapService.cs    // 가챠 카탈로그 + 서버 Bootstrap 동기화
    BootstrapModels.cs          // 샘플용 축약 Response 모델
  Catalog/
    CatalogService.cs           // meta 조회, SHA-256 검증, 로컬 캐시, 강제 갱신
    CatalogModels.cs            // 상점/가챠/아이템 카탈로그 모델
  Firebase/
    FirebaseService.cs          // Firebase Auth/AppCheck/Player API 초기화와 세션 정리
    FirebaseUtil.cs             // API data 토큰 DTO 변환
    ApiResponse.cs              // 공통 응답 포맷과 도메인 코드 상수
    PlayerApi.cs                // User/Shop/Gacha REST API 래퍼
    PlayerApiClient.cs          // 토큰 첨부, 401/AppCheck 재시도, 요청 큐 분기
    SendQueue.cs                // 상태 변경 요청 순차 실행 큐
    RequestIdRetryPolicy.cs     // requestId 멱등성 쓰기 요청의 공통 재시도 정책
  Shop/
    ShopPurchaseService.cs      // 구매 요청 + 재시도 + 구매 결과 캐시 반영
  Gacha/
    GachaDrawService.cs         // 뽑기 요청 + 재시도 + Draw 결과 캐시 반영

UI/
  UIView.cs                     // 공통 View 베이스
  Window/
    UIWindowService.cs          // scene/prefab window 관리, route stack 뒤로가기, 동시성 제어
    UIWindowService.Overlay.cs  // Reward overlay 생성·해제
    UIWindowService.CanvasSorting.cs // window/overlay Canvas 정렬 보정
    UIWindowType.cs
  Shop/                         // 상점 Presenter/View/ViewModel/SectionType
  Common/                       // ScriptableObject 프리셋, 공통 UI 컴포넌트

Utils/
  Log.cs
```

## 사용 기술

- Unity 6
- Firebase Auth / AppCheck / Cloud Functions REST API
- Addressable Asset
- UniTask
- R3
- Newtonsoft.Json
- ScriptableObject

## 주요 구현 포인트

- Additive Scene window와 Addressables prefab window를 하나의 서비스로 관리하는 아웃게임 UI 전환 구조
- route stack 기반 뒤로가기와 window 작업 동시성 제어(busy 상태, pending close-all)
- MVP 기반 Presenter/View 분리 구조
- 유저/상점/가챠/아이템 Bootstrap 병렬 로드와 중복 요청 병합(UniTaskCompletionSource)
- catalogVersion, SHA-256 해시 검증, 로컬 캐시를 활용한 카탈로그 관리
- 구매/뽑기 요청의 requestId 멱등성과 재시도 정책 분리(RequestIdRetryPolicy)
- catalogVersion 불일치 시 상점 영역만 강제 갱신하는 복구 흐름
- Firebase Player API 도메인별 클래스 분리, 401/AppCheck 오류 계약 기반 1회 재시도
- Addressables SpriteLease 패턴과 prefab instance 관리를 통한 리소스 수명 제어
- ScriptableObject 프리셋 기반 UI 스타일 데이터 관리(normal, rare, epic, legend 등 일반화된 등급명 사용)
