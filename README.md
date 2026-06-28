# SampleCode4

Unity 클라이언트 포트폴리오 공개용 C# 샘플 코드입니다.
아웃게임 UI, Firebase API 연동, 카탈로그/Bootstrap 데이터 갱신, Addressables 리소스 관리 구조를 보여주기 위해 MVP 기반 상점 흐름을 대표 샘플로 정리했습니다.

실제 서비스 URL, 토큰, 세부 정책, 전체 Response 모델, 프로젝트 고유 수치/공식은 제거하거나 `중략` 처리했습니다.
컬렉션, 가챠, 메일, 일일보상 등 같은 패턴으로 확장되는 콘텐츠 UI는 샘플에서 제외했습니다.

## 폴더 구조

```
Scene/
  PersistentSingleton.cs    // DontDestroyOnLoad 기반 공통 싱글턴
  SampleRuntime.cs         // 단독 씬 테스트용 EventSystem 보정
  SceneLoader.cs           // Additive Scene 로드/언로드 유틸리티
  SceneNames.cs            // 샘플에서 사용하는 Scene 이름 관리
  Main/MainFlow.cs         // 메인 진입 시 Bootstrap 로드 플로우

Service/
  Addressables/
    AddressableSpriteService.cs // 참조 카운트 기반 Sprite 로드/해제
  Bootstrap/
    AppBootstrapService.cs     // 유저/상점 런타임 캐시 및 상태 갱신
    ShopBootstrapService.cs     // 상점 카탈로그 + 서버 Bootstrap 동기화
    BootstrapModels.cs          // 공개 샘플용 축약 Response 모델
  Catalog/
    CatalogService.cs           // 원격 카탈로그 다운로드, SHA-256 검증, 로컬 캐시
    CatalogModels.cs            // 상점/아이템 카탈로그 모델
  Firebase/
    FirebaseService.cs          // Firebase Auth/AppCheck/Player API 초기화
    FirebaseUtil.cs             // API data 토큰 DTO 변환
    PlayerApi.cs                // User/Shop REST API 래퍼
    PlayerApiClient.cs          // Firebase 토큰 기반 HTTP 통신 계층
    SendQueue.cs                // 서버 요청 순차 실행 큐

UI/
  UIWindowService.cs            // Additive Scene 기반 UI 창 관리 샘플
  UIView.cs                     // 공통 View 베이스
  Store/                        // 상점 Presenter/View/ViewModel
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

- Additive Scene 기반 아웃게임 UI 전환 구조
- MVP 기반 Presenter/View 분리 구조
- 서버 카탈로그/Bootstrap 데이터 기반 콘텐츠 갱신
- catalogVersion, SHA-256 해시 검증, 로컬 캐시를 활용한 카탈로그 관리
- Firebase Player API 도메인별 클래스 분리
- 유저 재화, 인벤토리, 구매 상태 동기화
- Addressables SpriteLease 패턴을 통한 참조 카운트 기반 리소스 관리
- ScriptableObject 프리셋 기반 UI 스타일 데이터 관리(normal, rare, epic, legend 등 일반화된 등급명 사용)





