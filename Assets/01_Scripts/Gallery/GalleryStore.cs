using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 갤러리의 영구 저장소. 엔딩 한 편(크레딧 텍스트 + 이미지)을 로컬에 쌓고 다시 읽어온다.
///
/// 구조:
///   {persistentDataPath}/gallery/index.json        ← GalleryDatabase (추가 순서 = 시간 순서)
///   {persistentDataPath}/gallery/images/{id}.png   ← 엔트리별 이미지
///
/// 엔딩 이미지 캐시(<see cref="EndingImageGenerator.CacheDirectory"/>)를 참조만 하지 않고
/// PNG 를 복사해 넣는다 — 캐시는 조합 키로 덮어써지는 임시 저장소라 갤러리가 물릴 수 없다.
///
/// 저장에 실패해도 게임은 멈추면 안 된다. 모든 파일 IO 는 예외를 삼키고 경고만 남긴다.
/// </summary>
public static class GalleryStore
{
    /// <summary>갤러리 저장 폴더.</summary>
    public static string RootDirectory => Path.Combine(Application.persistentDataPath, "gallery");

    /// <summary>이미지가 쌓이는 폴더.</summary>
    public static string ImageDirectory => Path.Combine(RootDirectory, "images");

    /// <summary>인덱스 파일 경로.</summary>
    public static string IndexPath => Path.Combine(RootDirectory, "index.json");

    /// <summary>엔트리 이미지의 전체 경로.</summary>
    public static string ImagePathFor(GalleryEntry entry)
        => entry == null ? string.Empty : Path.Combine(ImageDirectory, entry.imageFile ?? string.Empty);

    /// <summary>
    /// 저장된 엔트리를 시간 순서(오래된 것 → 최신)로 돌려준다.
    /// 저장본이 없거나 읽을 수 없으면 빈 목록이다 — 갤러리는 비어 있는 상태로 열리면 된다.
    /// </summary>
    public static IReadOnlyList<GalleryEntry> LoadAll() => LoadDatabase().entries;

    /// <summary>
    /// 엔딩 한 편을 갤러리에 추가한다.
    /// 대본이나 이미지가 없으면 저장하지 않는다 — 갤러리는 텍스트와 이미지가 한 쌍일 때만 의미가 있다.
    /// </summary>
    /// <returns>저장된 엔트리. 저장하지 못했으면 null.</returns>
    public static GalleryEntry Save(EndingScript script, Texture2D image, RunResult result)
    {
        if (script == null || !script.IsValid)
        {
            Debug.LogWarning("GalleryStore: 대본이 유효하지 않아 저장하지 않습니다.");
            return null;
        }

        if (image == null)
        {
            Debug.LogWarning("GalleryStore: 이미지가 없어 저장하지 않습니다.");
            return null;
        }

        string combinationKey = result != null ? result.CombinationKey : string.Empty;
        var entry = new GalleryEntry
        {
            id = BuildId(combinationKey),
            savedAt = DateTime.Now.ToString("o"),
            creditLines = script.credit_lines,
            epilogue = script.epilogue,
            combinationKey = combinationKey,
            endedEarly = result != null && result.EndedEarly,
            endedAtEra = result != null ? result.EndedAtEra : string.Empty,
        };
        entry.imageFile = $"{entry.id}.png";

        string imagePath = Path.Combine(ImageDirectory, entry.imageFile);

        try
        {
            Directory.CreateDirectory(ImageDirectory);
            File.WriteAllBytes(imagePath, image.EncodeToPNG());
        }
        catch (Exception e)
        {
            Debug.LogWarning($"GalleryStore: 이미지 저장 실패 — {e.Message}");
            return null;
        }

        GalleryDatabase db = LoadDatabase();
        db.entries.Add(entry);

        if (!TryWriteDatabase(db))
        {
            // 인덱스에 못 올렸으면 이미지만 남아 봐야 읽을 길이 없다 — 되돌린다.
            TryDelete(imagePath);
            return null;
        }

        return entry;
    }

    /// <summary>엔트리의 이미지를 읽어온다. 파일이 없거나 깨졌으면 null.</summary>
    public static Texture2D LoadTexture(GalleryEntry entry)
    {
        string path = ImagePathFor(entry);
        if (string.IsNullOrEmpty(path))
            return null;

        try
        {
            if (!File.Exists(path))
                return null;

            // LoadImage 가 크기를 알아서 맞춰주므로 초기 크기는 의미가 없다.
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(File.ReadAllBytes(path)))
                return tex;

            UnityEngine.Object.Destroy(tex);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"GalleryStore: 이미지 로드 실패 — {e.Message}");
        }

        return null;
    }

    private static GalleryDatabase LoadDatabase()
    {
        try
        {
            if (!File.Exists(IndexPath))
                return new GalleryDatabase();

            var db = JsonUtility.FromJson<GalleryDatabase>(File.ReadAllText(IndexPath));

            // 파일이 비었거나 깨졌으면 FromJson 이 null 이나 빈 껍데기를 돌려준다.
            if (db == null)
                return new GalleryDatabase();

            if (db.entries == null)
                db.entries = new List<GalleryEntry>();

            return db;
        }
        catch (Exception e)
        {
            // 인덱스를 못 읽으면 갤러리는 비어 보이지만, 덮어써서 날려버리진 않는다.
            Debug.LogWarning($"GalleryStore: 인덱스 로드 실패 — {e.Message}");
            return new GalleryDatabase();
        }
    }

    private static bool TryWriteDatabase(GalleryDatabase db)
    {
        try
        {
            Directory.CreateDirectory(RootDirectory);
            File.WriteAllText(IndexPath, JsonUtility.ToJson(db, true));
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"GalleryStore: 인덱스 저장 실패 — {e.Message}");
            return false;
        }
    }

    /// <summary>시각 + 조합 키로 만드는 식별자. 파일명으로 쓰므로 영숫자와 언더스코어만 남긴다.</summary>
    private static string BuildId(string combinationKey)
        => $"{DateTime.Now:yyyyMMdd_HHmmss}_{StableHash.Of(combinationKey):X8}";

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"GalleryStore: 되돌리기 실패 — {e.Message}");
        }
    }
}
