엔딩 크레딧 생성에 쓰는 Gemini API 키를 이 폴더에 둔다.

  파일명: gemini_api_key.txt
  내용:   키 한 줄 (# 또는 // 로 시작하는 줄과 빈 줄은 무시된다)

이 파일은 .gitignore 에 등록돼 있어 저장소에 올라가지 않는다.
각자 https://aistudio.google.com/apikey 에서 발급받아 직접 넣을 것.

환경변수 GEMINI_API_KEY 를 설정해도 되며, 그쪽이 우선 적용된다.
키가 없어도 게임은 정상 동작한다 — 엔딩이 폴백 텍스트로 진행되고 이미지만 생략된다.

자세한 내용은 docs/Ending_LLM.md 참고.
