엔딩 크레딧 생성에 쓰는 Gemini API 키를 넣는 경로 중 하나다.

키 로딩 우선순위 (GeminiApiConfig.Load)
  1. 환경변수 GEMINI_API_KEY          — 개발 중 권장. 에디터에서만 적용된다
  2. Unity Remote Config              — 빌드의 기본 경로. 키 이름: Gemini_Youbean999
  3. 이 폴더의 gemini_api_key.txt     — 오프라인 폴백

이 폴더에 넣을 경우
  파일명: gemini_api_key.txt
  내용:   키 한 줄 (# 또는 // 로 시작하는 줄과 빈 줄은 무시된다)
  이 파일은 .gitignore 에 등록돼 있어 저장소에 올라가지 않는다.
  각자 https://aistudio.google.com/apikey 에서 발급받아 직접 넣을 것.

Remote Config 를 쓰면 이 파일은 없어도 된다.
빌드에 키를 동봉하고 싶지 않다면 파일을 지우고 대시보드에만 등록하면 된다.

키가 없어도 게임은 정상 동작한다 — 엔딩이 폴백 텍스트로 진행되고 이미지만 생략된다.
어느 경로에서 읽혔는지는 콘솔 로그로 확인할 수 있다.

자세한 내용은 docs/Ending_LLM.md 참고.
