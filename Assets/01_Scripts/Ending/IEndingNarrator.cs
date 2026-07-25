using System;
using System.Collections;

/// <summary>
/// 한 판의 결과를 엔딩 대본(<see cref="EndingScript"/>)으로 바꾸는 생성기.
/// 프로바이더(Gemini 등)를 갈아끼울 수 있도록 인터페이스로 분리한다.
/// 코루틴으로 동작하므로 호출 측이 StartCoroutine 으로 돌린다.
/// </summary>
public interface IEndingNarrator
{
    /// <summary>
    /// 결과를 대본으로 변환한다. 성공하면 <paramref name="onDone"/>, 실패하면 <paramref name="onFail"/> 를
    /// 정확히 한 번 호출해야 한다. 예외를 던져서는 안 된다 — 엔딩이 멈추면 안 되기 때문이다.
    /// </summary>
    IEnumerator Generate(RunResult result, Action<EndingScript> onDone, Action<string> onFail);
}
