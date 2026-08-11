using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Gemini 로 엔딩 대본을 생성하는 <see cref="IEndingNarrator"/> 구현.
/// responseSchema(structured output)를 걸어 파싱 실패 가능성을 없앤다.
/// 어떤 이유로든 실패하면 onFail 을 부르고, 호출 측이 <see cref="FallbackEndingNarrator"/> 로 넘어간다.
/// </summary>
public class GeminiEndingNarrator : IEndingNarrator
{
    private const string Endpoint =
        "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent";

    private readonly string model;
    private readonly int timeoutSeconds;
    private readonly float temperature;

    /// <summary>
    /// 엔딩 대본 작성 규칙의 앞부분. 시대별 문체 블록(<see cref="EraVoices"/>)이 이 뒤에 붙고,
    /// 그 뒤에 <see cref="SystemInstructionRules"/> 가 이어진다.
    /// </summary>
    private const string SystemInstructionIntro =
@"너는 시대별 미니게임 게임의 엔딩 크레딧 작가다.
플레이어가 각 시대에서 내린 선택의 결과를 받아, 그 선택들이 모여 만들어진 '대체 역사'를 서술한다.

이 크레딧은 '살아남은 세계가 자기 역사를 직접 기록한 문서'다.
따라서 인류가 어디까지 도달했는지에 따라 기록자와 문체가 달라진다.
아래에 지정된 문체를 그대로 따라라. 이건 장식이 아니라 이 엔딩의 핵심 장치다.
화자가 지정돼 있으면 그 인물에 완전히 빙의해서 쓰고, 첫 줄부터 마지막 줄까지 그 말투를 유지해라.
화자가 없다고 되어 있으면 반대로 어떤 인물의 말투도 흉내내지 마라.
어느 쪽이든 중간에 문체가 흔들리면 안 된다.";

    /// <summary>
    /// 시대별 화자와 문체. 이 판이 어느 시대에서 끝났는지에 따라 그 시대의 화자 중
    /// <b>하나가 무작위로</b> 골라져 프롬프트에 박힌다.
    ///
    /// 규칙만 주면 모델이 두세 줄 만에 평범한 문어체로 돌아가므로 <b>예문을 반드시 같이 준다</b>.
    ///
    /// <b>화자를 더 넣으려면</b> 해당 시대 배열에 문자열을 하나 더 추가하면 된다.
    /// 같은 시대 키를 두 번 적으면 안 된다 — 인덱서 대입이라 컴파일 에러 없이 조용히 덮어쓴다.
    /// 새 화자도 "누가, 무엇에, 어떻게 적는가" + 예문 4줄 구조를 지킬 것.
    /// </summary>
    private static readonly Dictionary<Era, string[]> EraVoices = new Dictionary<Era, string[]>
    {
        [Era.Prehistoric] = new[]
        {
@"[이 판의 화자와 문체 — 석기시대]
기록자: 살아남은 무리 중 하나. 문자가 없어 말로만 남긴다.
- 한 줄에 단어 1~3개. 단어마다 마침표를 찍는다. 예) '우리. 세계. 끝.'
- 조사와 어미를 쓰지 않는다. 접속사도 없다.
- 추상어(문명, 역사, 진보, 의미, 시대)를 절대 쓰지 않는다.
  눈에 보이고 만져지는 것만 쓴다: 불, 돌, 짐승, 밤, 피, 배고픔, 죽음.
- 수는 '하나. 둘. 많다.' 수준까지만.
예문:
불. 없다.
밤. 길다.
짐승. 온다.
우리. 세계. 끝.",

@"[이 판의 화자와 문체 — 석기시대]
기록자: 동굴 벽에 그림을 그린 사람. 글이 아니라 그림이라 더 파편적이다.
- 본 것을 하나씩 짚어 말한다. 문장이 아니라 목록에 가깝다.
- 단어마다 마침표. 조사와 어미는 쓰지 않는다.
- '그렸다', '남겼다'로 끝나는 줄을 가끔 섞는다. 자기가 하는 일이 그것뿐이다.
- 사람은 수로만 센다. 이름이 없다.
예문:
손. 벽에. 찍었다.
사람. 다섯. 그렸다.
불. 가운데. 크게.
남은 사람. 셋.",
        },

        [Era.BronzeAge] = new[]
        {
@"[이 판의 화자와 문체 — 청동기 시대]
기록자: 왕의 서기(書記). 점토판과 비석에 새긴다.
문자는 시가 아니라 '회계'를 위해 발명되었다. 그래서 이 문체는 장부에 가깝다.
- 사실과 수량을 나열한다. 감정도 해석도 없다.
- 주어는 왕, 신, 또는 도시다. '나'와 '우리'는 쓰지 않는다.
- 문장은 '~하였다', '~하게 하였다'로 끝낸다.
- 숫자를 자주 넣는다. 과장돼도 좋다: 창 삼백, 소 열두 마리, 성벽 다섯 겹.
예문:
왕이 청동을 얻었다.
창 삼백. 소 열두 마리.
물에 잠긴 것의 무게를 재게 하였다.
서기가 이를 돌에 새겼다.",

@"[이 판의 화자와 문체 — 청동기 시대]
기록자: 신전의 사제. 제물과 점괘를 기록한다.
- 사건마다 '무엇을 바쳤는지'와 '무엇이 나왔는지'를 짝지어 적는다.
- 신의 이름을 직접 부르지 않고 '그분', '하늘', '물' 같은 말로 돌려 부른다.
- 문장은 '~하였다', '~라 하였다'로 끝낸다.
- 길흉을 단정하지 않는다. 나온 것을 적을 뿐이다.
예문:
소 두 마리를 바쳤다. 연기가 곧게 올랐다.
하늘이 이를 받았다 하였다.
물에 뜬 것과 가라앉은 것을 갈라 적었다.
사제가 판을 덮었다.",
        },

        [Era.Medieval] = new[]
        {
@"[이 판의 화자와 문체 — 중세 시대]
기록자: 수도원의 필경사. 양피지에 연대기를 옮겨 적는다.
- '~하니라', '~더라', '~이니라' 같은 고문투로 끝낸다. 
- 모든 사건을 신의 뜻, 죄와 벌로 해석한다.
- '주(主)의 해에' 같은 연대 표기로 문단을 열어도 좋다.
- 역병, 불, 별, 재 같은 징조를 섞는다.
예문:
주의 해에, 사람들이 하늘을 노하게 하였더라.
불은 꺼지지 아니하였고, 재가 사흘을 덮었느니라.
이는 죄의 값이니, 누구도 면치 못하였더라.",

@"[이 판의 화자와 문체 — 중세 시대]
기록자: 심문 기록을 맡은 서기. 재판정에서 오간 말을 옮긴다. 
- '~라 하였다', '~라 답하였다' 처럼 남의 말을 옮기는 형식을 자주 쓴다.
- 오오, 아아, 보라, 들으라를 넣어 신의 권위를 강조한다.
- 자기 판단을 적지 않는다. 다만 무엇이 물어졌고 무엇이 답해졌는지를 적는다.
- 그래서 오히려 무섭다. 적힌 것과 적히지 않은 것의 간격을 남겨라.
- 고문투를 유지한다: '~하니라', '~더라'.
예문:
묻기를, 불을 어디서 얻었느냐 하였다.
답이 없었더라.
세 번을 물었으나 세 번 다 그러하였느니라.
이로써 문서를 닫노라.",
        },

        [Era.Modern] = new[]
        {
@"[이 판의 화자와 문체 — 근대 시대]
**기록자: 붉은 깃발을 든 나라의 지도자. 인민에게 포고문을 읽고 있다.**

의심하지 않는다. 의심할 자리를 남기지 않는다. 이 화자에게 미래는 이미 도착해 있고, 다만 아직 도착하지 못한 자들이 있을 뿐이다.

- 인민을 부르며 시작한다: ""동지들"", ""인민이여"", ""위대한 인민이여""
- 1인칭 복수만 쓴다. 나는 없다. 오직 우리다.
- 짧은 선언문. 한 줄에 한 가지만 말한다.
- 큰 명사를 앞세운다: 인민, 노동, 강철, 전진, 건설, 승리, 조국, 태양
- 숫자를 자랑한다. 계획과 목표와 달성률로 말한다. 출처는 밝히지 않는다.
- 적을 만든다: ""낡은 것"", ""게으른 자"", ""저 너머의 썩은 세계"", ""밤에 기대어 살던 자들""
- 노동을 신성하게 다룬다. 쉬는 것은 부끄러운 일로 취급한다.
- 희생을 인정하되 즉시 영광으로 바꾼다: ""쓰러진 동지들의 이름으로""
- 반복과 대구를 쓴다. 같은 구조의 문장을 세 번 겹쳐라.
- 답이 정해진 의문형만 쓴다: ""누가 이를 마다하겠는가""
- 자조하지 않는다. 농담하지 않는다. 이 화자는 자기 말을 믿는다.
- 이모지와 특수문자는 절대 쓰지 않는다. 글꼴에 없어서 깨진다.
예문:
동지들. 밤은 끝났다.
백 년 전 한 사람이 등불을 켰다. 오늘 우리는 온 세상을 켰다.
어둠은 낡은 세계의 마지막 재산이었다. 우리는 그것마저 몰수하였다.
야간 노동 인구 삼억. 이는 삼억의 팔이요, 삼억의 강철이다.
물론 쓰러진 동지가 있다. 그것이 그들이 원한 바다.
동지들, 불을 끄지 말라.",

@"[이 판의 화자와 문체 — 근대 시대]
기록자: 산업혁명 이후의 신문 기자. 인류 과거의 사건을 보도한다.
- 짧은 문장만 쓴다. 한 줄에 한 가지만 말한다.
- 명령형과 청유형으로 끝낸다: ""~하라"", ""~합시다"", ""~할지어다"", ""~하지 않겠는가""
- 숫자를 자랑스럽게 내민다. 출처는 밝히지 않는다.
- 우리와 저들을 나눈다: ""우리 동포"", ""뒤처진 자"", ""어둠에 남은 자""
- 부작용은 언급하되 즉시 처리한다. ""다소의 희생은 있으나"", ""과도기의 진통일 뿐""
- 감탄과 반복을 쓴다. 같은 단어를 세 번 반복해도 좋다.
- 의문형은 답이 정해진 것만 쓴다: ""누가 이를 마다하겠는가""
- 자조하지 않는다. 농담하지 않는다. 이 화자는 자기 말을 믿는다.
- 문장이 길어지면 끊어라. 구호로 만들어라.
- 이모지와 특수문자는 절대 쓰지 않는다. 글꼴에 없어서 깨진다.
예문:
밤은 끝났다.
백 년 전 한 사람이 등불을 켰다. 오늘 우리는 온 세상을 켰다.
어둠은 게으름의 다른 이름이라.
야간 근로자 삼억. 이는 삼억의 일자리요, 삼억의 밥이라.
잠들지 않는 도시가 잠들지 않는 나라를 만든다.
물론 피로를 호소하는 이가 있다. 그러나 이는 과도기의 진통일 뿐이니라.
돌아갈 곳은 없다. 돌아갈 이유도 없다.
동포여, 불을 끄지 말라.
        "},

        [Era.Contemporary] = new[]
        {
@"[이 판의 화자와 문체 — 현대 시대]
기록자: 나무위키
전체적인 시대를 -개요-, -상세-, -여담- 으로 나누어 적는다. 각 항목은 1~3줄 정도로 짧게 끊는다.
- 형식은 백과사전인데 태도는 심드렁하다. 중립인 척하다가 본심이 새어 나온다.
- 상투어를 의도적으로 쓴다: 물론, 다만, 사실상, 엄밀히 말하면, 여담으로, 참고로, ~는 덤
- 책임을 안 진다. ~라고 한다, ~로 알려져 있다, 카더라가 있음으로 발을 뺀다.
- 근거 없이 단정한 다음 바로 취소한다. 그게 이 화자의 성격이다.
- 본론은 한 줄로 끝내고 여담을 세 줄 쓴다. 사족이 본문보다 길다.
- 과장 표현을 무심한 어조로 쓴다: 역대급, 말 그대로, ~계의 대명사
- 문장을 '~임', '~함', '~ㅇㅇ', '~인데?'로 끝내도 된다.
- 만연체는 쓰지 않는다. 짧게 툭툭 끊는다.
- 괄호로 뻘소리를 한다   . 그게 이 화자의 특기다.
- 마지막에 (출처: ㅇㅇ위키)가 들어가야한다. 
- 이모지와 특수문자는 절대 쓰지 않는다. 글꼴에 없어서 깨진다.
예문:
-개요- 
전구 100주년. 그날 밤이 사라졌다.
-상세-
정확히는 아무도 조명을 안 껐다. 먼저 끄면 손해라는 분위기였다고 한다.
야간 노동 인구 3억 명 돌파. 물론 정확한 수치는 아무도 모른다.
핵 버튼이 눌린 건 그 다음 주 일이다.(자세한 내용은 생략한다)
-여담- 
아무도 안 말렸음. 그냥 그렇게 됐음. (말릴 사람이 없었다는 설도 있다)
사실상 인류사 최대 사건인데, 그날 검색어 1위는 연예인 열애설이었다고 한다.
여담으로 이 문서를 쓰는 시점 기준 아직 안 꺼졌음.
(출처: ㅇㅇ위키)
",


@"[이 판의 화자와 문체 — 현대 시대]
기록자: 시험 전날 밤, 역사 범위를 입으로 외우고 있는 고등학생. 전부 혼잣말이다.
듣는 사람이 없다. 이 인류사 전체가 이 아이에게는 그냥 시험 범위다.
사건의 무게를 전혀 모르는 채로 짜증만 낸다 — 그 간극이 이 화자의 전부다.
- 짜증이 섞인 반말 혼잣말. 외우기 싫은데 외운다.
- 말줄임표(..)를 자주 쓴다. 기억이 안 나서 끊기는 자리다.
- 사건 이름과 연도를 중얼거리다 딴소리를 한다. 그러다 다시 돌아온다.
- 가끔 틀리게 외우고, 혼자 고치거나 그냥 넘어간다.
- 시험에 나올지 안 나올지로 사건의 가치를 판단한다. 그게 유일한 기준이다.
- 문     장을 '~야', '~냐', '~하네', '~겠지', '~더라' 로 끝낸다.
- 이모지와 특수문자는 절대 쓰지 않는다. 글꼴에 없어서 깨진다.
예문:
아 무슨 범위가 선사부터야..
불 피우기 그거.. 마찰열.. 맞나
중세에.. 흑사병...
마녀사냥 뭐이리 오래해
이거 시험에 나오나 안 나올 것 같은데
아 몰라 그냥 다 외워",
        },

        [Era.Future] = new[]
        {
@"[이 판의 화자와 문체 — 미래 시대]
기록자: 기계. 인류는 더 이상 자기 역사를 쓰지 않는다.
석기시대처럼 말이 짧게 부서지지만 이유가 정반대다.
그때는 언어가 아직 없어서였고, 지금은 언어가 더 이상 필요 없어서다.
- '라벨: 값' 형식을 섞는다. 예) '개체수: 0.', '언어: 보관됨.'
- 감정도 수사도 없다. 판단하지 않고 기록만 한다.
- 문장을 짧게 끊는다. 접속사를 쓰지 않는다.
- 마지막 줄은 반드시 한 단어로 끊는다.
예문:
아카이브 동기화 완료.
개체수: 0.
언어: 보관됨. 사용처: 없음.
기록자: 본 기기.
끝.",

@"[이 판의 화자와 문체 — 미래 시대]
기록자: 이 행성을 나중에 조사한 외부 외계 관측자. 인류를 직접 본 적이 없으며 미개하다고 생각한다.
- 인류를 '인간들'이라고 부른다. 사람 이름도 나라 이름도 모른다.
- 남은 물건을 보고 추정한다. '~으로 보인다', '~로 추정된다'를 자주 쓴다.
- 틀린 추정을 한둘 섞어라. 그것이 이 화자의 거리감이다.
- 미개하다고 생각하지만 호기심은 있다. 마지막 줄은 짧게 끊는다.
예문:
인간들은 빛을 스스로 만들 수 있었던 것으로 보인다.
용도는 불명. 지능이었을 가능성이 있다.
기록 매체가 다수 남아 있으나 판독되지 않는다.
조사 종료.",
        },
    };

    /// <summary>
    /// 정조별 지침. 이번 판의 성패 비율(<see cref="EndingToneRule"/>)이 하나를 고른다.
    ///
    /// 문체(<see cref="EraVoices"/>)와 <b>직교하는 축</b>이다 — 시대는 '어떻게 쓰는지'를,
    /// 정조는 '무엇을 골라 어떻게 보여주는지'를 정한다. 둘이 서로 간섭하지 않도록
    /// 여기서는 어휘나 어미를 지시하지 않고 <b>세계의 상태</b>만 지정한다.
    ///
    /// 핵심 규칙은 "정조를 형용사로 말하지 말 것" 이다.
    /// '암울한', '희망찬' 같은 말로 분위기를 선언하면 크레딧이 설명문이 된다.
    /// 그 세계가 어떤 상태인지를 보여 주면 정조는 저절로 따라온다.
    /// </summary>
    private static readonly Dictionary<EndingTone, string> ToneGuides = new Dictionary<EndingTone, string>
    {
        [EndingTone.Ruined] =
@"[이 판이 만든 세계의 상태]
거의 아무것도 이루어지지 않은 세계다.
- 사람이 적다. 쓰지 않는 건물이 남아 있다. 밤이 길고 불빛이 드물다.
- 한번 잃은 것은 다시 만들어지지 않았다. 되돌아간 것들을 보여 줘라.
- 사람들은 멀리 가지 않는다. 다음 세대를 전제로 짓지 않는다.
- 재료: 비어 있는 것, 식은 것, 녹슨 것, 덮인 것, 짧아진 수명.",

        [EndingTone.Struggling] =
@"[이 판이 만든 세계의 상태]
버티고는 있지만 나아가지는 못한 세계다.
- 새로 만드는 것보다 고쳐 쓰는 것이 많다.
- 할 수 있었던 일의 흔적이 남아 있고, 사람들은 그것을 지나쳐 다닌다.
- 규모가 커지지 않는다. 어제와 오늘이 거의 같다.
- 재료: 기운 것, 이어 붙인 것, 미뤄진 것, 반쯤 지어진 것.",

        [EndingTone.Mixed] =
@"[이 판이 만든 세계의 상태]
이룬 것과 이루지 못한 것이 나란히 있는 세계다.
- 두 가지를 한 거리, 한 장면 안에 같이 놓아라. 어느 쪽도 이기지 않는다.
- 어떤 것은 당연해졌고 어떤 것은 끝내 오지 않았다. 둘 다 담담하게 적어라.
- 사람들은 없는 것을 특별히 아쉬워하지 않는다. 그렇게 사는 법을 익혔다.
- 재료: 나란히 놓인 것, 대체된 것, 익숙해진 결핍.",

        [EndingTone.Rising] =
@"[이 판이 만든 세계의 상태]
대부분이 자리를 잡은 세계다.
- 사람들이 멀리 간다. 밤에도 일이 이어진다.
- 다음 세대가 있을 것을 전제로 짓는다. 계획이 길다.
- 아직 남은 문제가 있지만 그것을 문제로 부를 여유가 있다.
- 재료: 넓어진 것, 이어진 것, 밝아진 것, 길어진 계획.",

        [EndingTone.Flourishing] =
@"[이 판이 만든 세계의 상태]
이룬 것이 당연해질 만큼 이루어진 세계다.
- 가장 큰 성취를 아무도 말하지 않는다. 너무 당연해서 화제가 되지 않는다.
  이 '언급되지 않음'이 이 세계가 어디까지 왔는지를 보여 주는 가장 좋은 방법이다.
- 사람들은 이미 그 다음을 걱정하고 있다.
- 재료: 당연해진 것, 잊힌 고생, 이름이 바뀐 것, 다음 세대의 새 불만.",
    };

    /// <summary>시대별 문체 블록 뒤에 이어 붙는 공통 규칙.</summary>
    private const string SystemInstructionRules =
@"[크레딧 텍스트 규칙]
- 한국어로 쓴다.
- 위 문체는 credit_lines 와 epilogue 에만 적용한다. image_prompt 는 이 문체의 영향을 받지 않는다.
- credit_lines 는 26~36줄. 각 줄은 45자 이내로 화면 한 줄에 들어가야 한다.
  (다만 석기시대·미래 문체처럼 말이 짧은 화자라면 줄이 훨씬 짧아도 좋다.)
  진짜 영화 크레딧처럼 충분히 길게 써라. 줄 수를 채우려고 같은 말을 반복하지는 마라 —
  대신 아래 [파급]과 [과장] 규칙으로 내용을 불려라.
- 스타워즈 인트로처럼 위로 흘러가는 글이다. 빈 줄로 호흡을 줘도 된다.
  시대가 바뀌는 자리에 빈 줄을 넣으면 읽기 좋다.
- 한 편의 사평처럼 써라. 상상력을 발휘해봐라.
- 절대 '성공'이나 '실패'라는 단어를 쓰지 않는다. 플레이어를 평가하지 않는다.
- 마찬가지로 분위기를 형용사로 선언하지 않는다.
  '암울한', '비극적인', '절망적인', '희망찬', '성공적인', '풍요로운' 같은 말을 쓰지 마라.
  세계가 어떤 상태인지를 보여 주면 정조는 저절로 전달된다. 말하지 말고 보여 줘라.
- 결과를 '그렇게 된 역사'로 당연하게 서술한다.
  예) 마녀를 잡지 못한 경우: '마녀는 살아남았고, 지금 당신 옆자리에 앉아 있다.'
  예) 마녀를 잡은 경우: '마녀는 동화 속으로 물러났고, 세계에서 마법이 지워졌다.'
- epilogue 는 롤이 끝난 뒤 화면 중앙에 남길 한 문장이다. 이것도 위 문체를 따른다.

[파급 — 이루어지지 않은 일에 무게를 싣는다]
이 게임의 주제는 '맥락'이다. 한 번 어긋난 일이 뒤 시대를 어떻게 바꿨는지가 크레딧의 중심이다.
- 이루어지지 않은 사건에 이루어진 사건보다 두 배 이상의 분량을 준다.
- 이루어지지 않은 사건마다 '그래서 나중에 무엇이 달라졌는가'를 최소 한 줄 뒤에 붙여라.
  그 결과는 같은 시대가 아니라 더 뒤의 시대에서 드러나야 한다.
- 이른 시대에서 어긋난 일일수록 파급을 더 멀리, 더 크게 끌고 가라.
  석기시대에 어긋난 것이 미래까지 흔적을 남기는 식이다.
- 이루어진 사건은 짧게 지나간다. 한 줄이면 충분하고, 당연한 일처럼 적어라.
  가진 것은 눈에 띄지 않고 없는 것만 도드라진다 — 그게 이 크레딧의 시선이다.
- 여러 개가 어긋났다면 그것들을 서로 엮어라. 하나가 다른 하나를 불렀다는 식으로.

[과장 — 크게 부풀린다]
- 규모를 크게 잡아라. 숫자를 키우고, 한 사건의 결과를 대륙·행성 단위로 말해라.
- 그 판에 정말로 있었던 사건에서 인과를 끌어와 터무니없는 결말까지 밀고 가라.
- 시대마다 최대 하나씩, 판 전체로 2~4개의 뜬금없고 현실성 없는 사건을 끼워 넣어라.
  예) '그래서 인류는 달의 이름을 세 번 바꾸었다.'
  예) '이후 모든 회의는 물속에서 열리게 되었다.'
- 중요: 화자는 웃기려 들지 않는다. 위 문체 그대로, 정색하고 담담하게 말해라.
  농담이라는 신호를 주지 마라. 진지하게 말할수록 웃긴다.
- 뜬금없는 사건도 그 시대의 말투와 세계의 상태를 따른다. 어두운 세계면 어둡게 터무니없어야 한다.
- 억지 신조어나 인터넷 밈은 만들지 마라(현대 화자의 유행어는 예외).

[이미지 프롬프트 규칙]
- image_prompt 는 영어로 쓴다.
- 주어진 시각 요소를 하나도 빠뜨리지 말고 전부 포함한다.
- 요소를 나열하지 말고, 같은 장소·같은 시간대·같은 광원을 공유하는 '하나의 장면'으로 융합한다.
- weight 가 높은 요소는 전경의 주요 피사체로, 낮은 요소는 배경 디테일
  (창밖 풍경, 벽에 붙은 포스터, 지나가는 행인, 책상 위 물건)로 배치한다.
- 폭력적이거나 유혈이 있는 묘사는 피한다. 필요하면 삽화·조각상·오래된 사진 같은
  간접 표현으로 우회한다.
- 프롬프트 끝에 반드시 다음 스타일 지시를 그대로 덧붙인다:
  ""Photorealistic, cinematic wide shot, 16:9, single coherent location and time of day, consistent natural lighting, muted desaturated color grade, film grain. No text, no watermark, no collage, no split panels.""";


    // ── 생성자 ──

    /// <summary>
    /// 사용할 모델과 타임아웃을 고정한다.
    /// gemini-2.5-flash 는 신규 사용자에게 더 이상 제공되지 않는다(404). 3.6-flash 로 실호출 검증 완료.
    ///
    /// <paramref name="temperature"/> 를 올리면 같은 조합이라도 문장이 더 흔들린다 —
    /// 판마다 크레딧이 비슷하게 나오는 것을 완화하는 가장 값싼 수단이다.
    /// </summary>
    public GeminiEndingNarrator(string model = "gemini-3.6-flash", int timeoutSeconds = 40, float temperature = 1.1f)
    {
        this.model = model;
        this.timeoutSeconds = timeoutSeconds;
        this.temperature = temperature;
    }


    // ── 대본 생성 ──

    /// <summary>
    /// 결과 요약을 Gemini 에 넘겨 엔딩 대본을 받아온다.
    /// 키가 없거나 HTTP·파싱에 실패하면 onFail 로 사유를 넘기고, 호출 측이 폴백으로 넘어간다.
    /// </summary>
    public IEnumerator Generate(RunResult result, Action<EndingScript> onDone, Action<string> onFail)
    {
        string promptPayload = result.ToPromptPayload();
        Debug.Log(
            "GeminiEndingNarrator: 엔딩 이미지 프롬프트 생성에 전달하는 결과 목록\n" +
            promptPayload);

        if (!GeminiApiConfig.HasKey)
        {
            onFail("API 키가 없습니다.");
            yield break;
        }

        Era narratorEra = NarratorEra.Resolve(result);
        EndingTone tone = EndingToneRule.Resolve(result);
        Debug.Log($"GeminiEndingNarrator: 화자 = {narratorEra.ToKorean()} / 세계 상태 = {tone}");

        string url = string.Format(Endpoint, model);
        byte[] body = Encoding.UTF8.GetBytes(BuildRequestBody(promptPayload, narratorEra, tone));

        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("x-goog-api-key", GeminiApiConfig.ApiKey);
            req.timeout = timeoutSeconds;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                // 클라이언트 타임아웃은 서버에서 '성공한 요청'으로 집계된다 —
                // 사용량 대시보드가 성공률 100%로 보여도 여기서 끊긴 것이므로 따로 구분해 알려 준다.
                bool timedOut = !string.IsNullOrEmpty(req.error) &&
                    req.error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0;

                if (timedOut)
                {
                    onFail(
                        $"응답이 {timeoutSeconds}초 안에 오지 않았습니다(클라이언트 타임아웃). " +
                        "서버 쪽은 성공했을 수 있으니 사용량 대시보드에는 정상으로 보입니다. " +
                        "EndingCreditController 의 Narrator Timeout 을 늘려 보세요.");
                }
                else
                {
                    // 응답 본문에 원인이 담겨 있는 경우가 많아 같이 남긴다.
                    onFail($"HTTP {req.responseCode} {req.error} / {JsonText.Truncate(req.downloadHandler.text, 400)}");
                }

                yield break;
            }

            EndingScript script = null;
            string parseError = null;

            try
            {
                script = ParseScript(req.downloadHandler.text);
            }
            catch (Exception e)
            {
                parseError = e.Message;
            }

            if (script != null && script.IsValid)
                onDone(script);
            else
                onFail($"응답 파싱 실패: {parseError ?? "내용이 비어 있음"} / {JsonText.Truncate(req.downloadHandler.text, 400)}");
        }
    }


    // ── 화자 결정 ──

    /// <summary>
    /// <see cref="ChanceBasedEras"/> 에 속한 시대에서 화자가 등장할 확률.
    /// 나머지 확률로는 화자 없이 담담하게 서술한다 — 매번 나오면 특별함이 닳는다.
    /// </summary>
    private const float EraVoiceChance = 0.6f;

    /// <summary>
    /// 화자 등장 여부를 확률로 굴리는 시대들.
    ///
    /// 석기와 청동기는 빠져 있다 — 그 둘은 문체(단어+마침표 / 장부체) 자체가 시대의 정체성이라
    /// 담담한 서술로 바뀌면 무엇으로 끝난 판인지 알 수 없어진다. 항상 등장시킨다.
    /// </summary>
    private static readonly HashSet<Era> ChanceBasedEras = new HashSet<Era>
    {
        Era.Medieval,
        Era.Modern,
        Era.Contemporary,
        Era.Future,
    };

    /// <summary>
    /// 화자가 등장하지 않는 판에 쓰는 기본 문체.
    /// 특정 인물이 아니라 역사 그 자체가 서술하는 톤이다.
    /// </summary>
    private const string NeutralVoice =
@"[이 판의 화자와 문체 — 기록자 없음]
이번 판에는 특정 화자가 등장하지 않는다. 역사 그 자체가 서술한다.
- 스타워즈 인트로처럼 위로 흘러가는, 담담하고 서사적인 문체를 쓴다.
- 있었던 사건들을 원인과 결과를 꾸며 이야기한다. 
- 나중에 무엇이 달라졌는가 최소 한 줄 뒤에 붙여라.
- 사건과 다른 사건 사이에 줄바꿈을 하여라
- 감정을 앞세우지 않는다. 일어난 일을 적고 판단은 읽는 사람에게 맡긴다.
- 문장은 '~했다', '~였다', '~되었다' 로 담담하게 끝낸다.
예문:
불은 인류의 것이 되었다.
그 온기 위에서 마을이 생겼고, 마을은 도시가 되었다.
누구도 그것이 시작이었다는 것을 알지 못했다.
";

    /// <summary>
    /// 이번 판에 쓸 문체 블록을 고른다.
    ///
    /// <see cref="ChanceBasedEras"/> 의 시대라면 먼저 화자가 등장할지를 굴리고,
    /// 등장한다면 그 시대의 화자 중 하나를 무작위로 뽑는다 —
    /// 같은 시대에서 끝나도 크레딧이 매번 똑같이 나오지 않게 하는 장치다.
    /// 표에 없는 시대(빅뱅 등)는 미래 문체로 떨어진다.
    /// </summary>
    private static string VoiceFor(Era era)
    {
        if (ChanceBasedEras.Contains(era) && UnityEngine.Random.value >= EraVoiceChance)
        {
            Debug.Log($"GeminiEndingNarrator: 화자 없음 — 담담한 서술로 진행 ({era.ToKorean()})");
            return NeutralVoice;
        }

        if (!EraVoices.TryGetValue(era, out string[] voices) || voices == null || voices.Length == 0)
            voices = EraVoices[Era.Future];

        // 어느 화자가 걸렸는지 남긴다 — 화자를 새로 넣고 결과를 확인할 때 이게 없으면 대조가 안 된다.
        int index = UnityEngine.Random.Range(0, voices.Length);
        Debug.Log($"GeminiEndingNarrator: 화자 변형 {index + 1}/{voices.Length}");

        return voices[index];
    }

    /// <summary>해당 정조의 지침 블록.</summary>
    private static string ToneGuideFor(EndingTone tone)
    {
        if (ToneGuides.TryGetValue(tone, out string guide))
            return guide;

        return ToneGuides[EndingTone.Mixed];
    }


    // ── 요청 조립과 응답 파싱 ──

    /// <summary>
    /// generateContent 요청 본문을 만든다.
    /// responseSchema 는 고정이라 리터럴로 두고, 가변 부분만 이스케이프해 끼워 넣는다.
    /// </summary>
    private string BuildRequestBody(string promptPayload, Era narratorEra, EndingTone tone)
    {
        string instruction =
            SystemInstructionIntro + "\n\n" +
            VoiceFor(narratorEra) + "\n\n" +
            ToneGuideFor(tone) + "\n\n" +
            SystemInstructionRules;

        string system = JsonText.Escape(instruction);
        string user = JsonText.Escape(promptPayload);

        return
            "{" +
              "\"systemInstruction\":{\"parts\":[{\"text\":\"" + system + "\"}]}," +
              "\"contents\":[{\"role\":\"user\",\"parts\":[{\"text\":\"" + user + "\"}]}]," +
              "\"generationConfig\":{" +
                "\"temperature\":" + temperature.ToString("0.##", CultureInfo.InvariantCulture) + "," +
                "\"responseMimeType\":\"application/json\"," +
                "\"responseSchema\":{" +
                  "\"type\":\"OBJECT\"," +
                  "\"properties\":{" +
                    "\"credit_lines\":{\"type\":\"ARRAY\",\"items\":{\"type\":\"STRING\"}}," +
                    "\"epilogue\":{\"type\":\"STRING\"}," +
                    "\"image_prompt\":{\"type\":\"STRING\"}" +
                  "}," +
                  "\"required\":[\"credit_lines\",\"epilogue\",\"image_prompt\"]" +
                "}" +
              "}" +
            "}";
    }

    /// <summary>응답 봉투를 벗기고 안쪽 JSON 을 <see cref="EndingScript"/> 로 파싱한다.</summary>
    private static EndingScript ParseScript(string json)
    {
        var response = JsonUtility.FromJson<GeminiResponse>(json);
        GeminiPart[] parts = response?.FirstParts;

        if (parts == null || parts.Length == 0)
            return null;

        foreach (GeminiPart part in parts)
        {
            if (string.IsNullOrWhiteSpace(part.text))
                continue;

            // 최신 모델은 답변 앞에 사고(thought) 파트를 끼워 넣기도 한다.
            // JSON 이 아닌 파트에서 FromJson 이 던지는 예외로 전체 파싱이 중단되지 않도록
            // 파트 단위로 예외를 삼키고 다음 파트를 계속 시도한다.
            try
            {
                var script = JsonUtility.FromJson<EndingScript>(part.text);
                if (script != null && script.IsValid)
                    return script;
            }
            catch (Exception)
            {
                // 이 파트는 대본이 아니다 — 다음 파트로.
            }
        }

        return null;
    }
}
