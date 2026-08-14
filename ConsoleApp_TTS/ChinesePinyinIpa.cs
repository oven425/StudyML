using System.Text;
using ToolGood.Words;

namespace ConsoleApp_TTS;

/// <summary>
/// 純 C# 的中文/英文 -> IPA 音素轉換器（不依賴 Python）。
/// 中文流程：漢字 -> 拼音 (ToolGood.Words) -> 聲母/韻母拆解 -> IPA (仿照 misaki/pinyin-to-ipa 的對照表)。
/// 英文流程：交給 <see cref="EnglishArpabetIpa"/>（內建 CMU 字典 + ARPAbet-IPA 對照表）。
/// 數字、符號等仍採「原樣通過」的簡易處理。
/// </summary>
public static class ChinesePinyinIpa
{
    // 帶聲調符號的母音 -> (無聲調母音, 聲調 1~4)。找不到聲調符號時視為第五聲(輕聲)。
    private static readonly Dictionary<char, (char Base, int Tone)> ToneMarks = new()
    {
        ['ā'] = ('a', 1), ['á'] = ('a', 2), ['ǎ'] = ('a', 3), ['à'] = ('a', 4),
        ['ē'] = ('e', 1), ['é'] = ('e', 2), ['ě'] = ('e', 3), ['è'] = ('e', 4),
        ['ī'] = ('i', 1), ['í'] = ('i', 2), ['ǐ'] = ('i', 3), ['ì'] = ('i', 4),
        ['ō'] = ('o', 1), ['ó'] = ('o', 2), ['ǒ'] = ('o', 3), ['ò'] = ('o', 4),
        ['ū'] = ('u', 1), ['ú'] = ('u', 2), ['ǔ'] = ('u', 3), ['ù'] = ('u', 4),
        ['ǖ'] = ('ü', 1), ['ǘ'] = ('ü', 2), ['ǚ'] = ('ü', 3), ['ǜ'] = ('ü', 4),
    };

    // Kokoro/misaki 使用的聲調符號（retone 後的結果）；第五聲(輕聲)不加符號。
    private static readonly Dictionary<int, string> ToneSymbol = new()
    {
        [1] = "→",
        [2] = "↗",
        [3] = "↓",
        [4] = "↘",
        [5] = "",
    };

    // 聲母 -> IPA（取 pinyin-to-ipa 對照表中的第一個變體）
    private static readonly Dictionary<string, string> InitialMap = new()
    {
        ["b"] = "p", ["p"] = "pʰ", ["m"] = "m", ["f"] = "f",
        ["d"] = "t", ["t"] = "tʰ", ["n"] = "n", ["l"] = "l",
        ["g"] = "k", ["k"] = "kʰ", ["h"] = "x",
        ["j"] = "tɕ", ["q"] = "tɕʰ", ["x"] = "ɕ",
        ["zh"] = "ʈʂ", ["ch"] = "ʈʂʰ", ["sh"] = "ʂ", ["r"] = "ɻ",
        ["z"] = "ts", ["c"] = "tsʰ", ["s"] = "s",
    };

    private static readonly string[] TwoLetterInitials = { "zh", "ch", "sh" };
    private static readonly string[] OneLetterInitials =
        { "b", "p", "m", "f", "d", "t", "n", "l", "g", "k", "h", "j", "q", "x", "r", "z", "c", "s" };

    // 韻母 -> IPA（'0' 是聲調符號要插入的位置佔位符）
    private static readonly Dictionary<string, string> FinalMap = new()
    {
        ["a"] = "a0", ["ai"] = "ai̯0", ["an"] = "a0n", ["ang"] = "a0ŋ", ["ao"] = "au̯0",
        ["e"] = "ɤ0", ["ei"] = "ei̯0", ["en"] = "ə0n", ["eng"] = "ə0ŋ",
        ["i"] = "i0", ["ia"] = "ja0", ["ian"] = "jɛ0n", ["iang"] = "ja0ŋ", ["iao"] = "jau̯0",
        ["ie"] = "je0", ["in"] = "i0n", ["iou"] = "jou̯0", ["ing"] = "i0ŋ", ["iong"] = "jʊ0ŋ",
        ["ong"] = "ʊ0ŋ", ["ou"] = "ou̯0",
        ["u"] = "u0", ["uei"] = "wei̯0", ["ua"] = "wa0", ["uai"] = "wai̯0", ["uan"] = "wa0n",
        ["uen"] = "wə0n", ["uang"] = "wa0ŋ", ["ueng"] = "wə0ŋ", ["uo"] = "wo0", ["o"] = "wo0",
        ["ü"] = "y0", ["üe"] = "ɥe0", ["üan"] = "ɥɛ0n", ["ün"] = "y0n",
    };

    // zh/ch/sh/r 後面的 "i" 用捲舌元音；z/c/s 後面的 "i" 用平舌元音
    private static readonly Dictionary<string, string> FinalAfterRetroflex = new() { ["i"] = "ɻ̩0" };
    private static readonly Dictionary<string, string> FinalAfterDental = new() { ["i"] = "ɹ̩0" };

    // 零聲母時 y/w 的拼寫 -> 對應的標準韻母 key
    private static readonly Dictionary<string, string> ZeroInitialSpelling = new()
    {
        ["yi"] = "i", ["ya"] = "ia", ["yao"] = "iao", ["ye"] = "ie", ["you"] = "iou",
        ["yan"] = "ian", ["yin"] = "in", ["yang"] = "iang", ["ying"] = "ing", ["yong"] = "iong",
        ["yu"] = "ü", ["yue"] = "üe", ["yuan"] = "üan", ["yun"] = "ün",
        ["wu"] = "u", ["wa"] = "ua", ["wo"] = "uo", ["wai"] = "uai", ["wei"] = "uei",
        ["wan"] = "uan", ["wen"] = "uen", ["wang"] = "uang", ["weng"] = "ueng",
    };

    // 音節式輔音 (嗯/呣 等)
    private static readonly Dictionary<string, string> SyllabicConsonant = new()
    {
        ["m"] = "m0", ["n"] = "n0", ["ng"] = "ŋ0",
    };

    /// <summary>把整段中文（可包含標點、英文、數字）轉成 Kokoro 使用的 IPA 音素字串。</summary>
    public static string TextToIpa(string text)
    {
        text = MapPunctuation(text);
        var sb = new StringBuilder();
        var englishWord = new StringBuilder();

        void FlushEnglishWord()
        {
            if (englishWord.Length == 0)
            {
                return;
            }
            sb.Append(EnglishArpabetIpa.WordToIpa(englishWord.ToString()));
            englishWord.Clear();
        }

        foreach (var ch in text)
        {
            if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || ch == '\'')
            {
                // 累積連續的英文字母（含縮寫用的單引號），湊成完整單字再查 CMU 字典
                englishWord.Append(ch);
                continue;
            }
            FlushEnglishWord();

            if (IsChineseChar(ch))
            {
                var pinyinList = WordsHelper.GetAllPinyin(ch, tone: true);
                var pinyin = pinyinList.Count > 0 ? pinyinList[0] : null;
                if (pinyin != null)
                {
                    sb.Append(SyllableToIpa(pinyin));
                    continue;
                }
            }

            // 其餘字元（標點、空白、數字）原樣通過
            sb.Append(ch);
        }
        FlushEnglishWord();

        return sb.ToString();
    }

    private static bool IsChineseChar(char ch) => ch is (>= '\u3400' and <= '\u9fd5');

    /// <summary>把單一帶調符拼音音節（例如 "zhōng"）轉成 IPA。</summary>
    private static string SyllableToIpa(string pinyinWithTone)
    {
        // ToolGood.Words 回傳的音節首字母是大寫（例如 "Wū"），統一轉小寫再解析
        var (normal, tone) = NormalizeTone(pinyinWithTone.ToLowerInvariant());
        string toneSymbol = ToneSymbol.TryGetValue(tone, out var sym) ? sym : "";

        if (SyllabicConsonant.TryGetValue(normal, out var syllabic))
        {
            return syllabic.Replace("0", toneSymbol);
        }

        // 找聲母
        string? initial = null;
        string remainder = normal;
        foreach (var two in TwoLetterInitials)
        {
            if (normal.StartsWith(two, StringComparison.Ordinal))
            {
                initial = two;
                remainder = normal[two.Length..];
                break;
            }
        }
        if (initial is null)
        {
            foreach (var one in OneLetterInitials)
            {
                if (normal.StartsWith(one, StringComparison.Ordinal))
                {
                    initial = one;
                    remainder = normal[one.Length..];
                    break;
                }
            }
        }

        string finalKey;
        if (initial is null)
        {
            // 零聲母：可能是 y/w 拼寫或直接的韻母（a, e, ai, an ...）
            if (ZeroInitialSpelling.TryGetValue(normal, out var mapped))
            {
                finalKey = mapped;
            }
            else
            {
                finalKey = normal;
            }
        }
        else
        {
            finalKey = remainder;

            // j/q/x 後面的 u 實際上是 ü（拼音省略了兩點）
            if ((initial is "j" or "q" or "x") && finalKey.StartsWith("u", StringComparison.Ordinal))
            {
                finalKey = "ü" + finalKey[1..];
            }

            // 常見的縮寫拼寫：iu -> iou, ui -> uei, un -> uen
            finalKey = finalKey switch
            {
                "iu" => "iou",
                "ui" => "uei",
                "un" => "uen",
                _ => finalKey,
            };
        }

        string finalIpa;
        if (finalKey == "i" && initial is "zh" or "ch" or "sh" or "r")
        {
            finalIpa = FinalAfterRetroflex["i"];
        }
        else if (finalKey == "i" && initial is "z" or "c" or "s")
        {
            finalIpa = FinalAfterDental["i"];
        }
        else if (FinalMap.TryGetValue(finalKey, out var fi))
        {
            finalIpa = fi;
        }
        else
        {
            // 無法辨識的韻母：原樣輸出，避免整段掛掉
            finalIpa = finalKey + "0";
        }

        string initialIpa = initial != null && InitialMap.TryGetValue(initial, out var ii) ? ii : "";
        return initialIpa + finalIpa.Replace("0", toneSymbol);
    }

    private static (string Normal, int Tone) NormalizeTone(string pinyin)
    {
        var sb = new StringBuilder(pinyin.Length);
        int tone = 5;
        foreach (var ch in pinyin)
        {
            if (ToneMarks.TryGetValue(ch, out var info))
            {
                sb.Append(info.Base);
                tone = info.Tone;
            }
            else if (char.IsDigit(ch))
            {
                // 保底：若拿到的拼音已經是數字聲調（例如 "zhong1"），直接採用
                tone = ch - '0';
            }
            else
            {
                sb.Append(ch);
            }
        }
        return (sb.ToString(), tone);
    }

    /// <summary>把中文全形標點轉成 Kokoro 詞彙表看得懂的半形標點（參考 misaki 的 map_punctuation）。</summary>
    private static string MapPunctuation(string text)
    {
        return text
            .Replace('、', ',').Replace('，', ',')
            .Replace('。', '.').Replace('．', '.')
            .Replace('！', '!')
            .Replace('：', ':')
            .Replace('；', ';')
            .Replace('？', '?')
            .Replace('«', '“').Replace('»', '”')
            .Replace('《', '“').Replace('》', '”')
            .Replace('「', '“').Replace('」', '”')
            .Replace('【', '“').Replace('】', '”')
            .Replace('（', '(').Replace('）', ')');
    }
}
