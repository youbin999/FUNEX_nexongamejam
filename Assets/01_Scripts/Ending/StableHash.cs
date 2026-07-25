/// <summary>
/// 실행을 넘어 값이 유지되는 문자열 해시(FNV-1a).
///
/// string.GetHashCode 는 실행마다(그리고 플랫폼마다) 값이 달라질 수 있어
/// 파일명·캐시 키·이미지 시드처럼 "다음에 켰을 때도 같아야 하는" 곳에는 쓸 수 없다.
/// 그런 용도는 전부 이 클래스를 거친다.
/// </summary>
public static class StableHash
{
    private const uint Offset = 2166136261;
    private const uint Prime = 16777619;

    /// <summary>문자열을 32비트 해시로 접는다. null 과 빈 문자열은 같은 값이다.</summary>
    public static uint Of(string value)
    {
        unchecked
        {
            uint hash = Offset;
            foreach (char c in value ?? string.Empty)
            {
                hash ^= c;
                hash *= Prime;
            }

            return hash;
        }
    }

    /// <summary>이미지 생성 시드처럼 양수 int 가 필요한 곳에 쓴다.</summary>
    public static int Seed(string value) => (int)(Of(value) & 0x7FFFFFFF);
}
