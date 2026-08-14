using System.Reflection;
using System.Text;

namespace ConsoleApp_TTS;

/// <summary>
/// 純 C# 的英文 -> IPA 音素轉換器（不依賴 Python / espeak）。
/// 做法：內建精簡版 CMU Pronouncing Dictionary（單字 -> ARPAbet 音素），查到就轉成 IPA；
/// 查不到的生字（人名、縮寫、新詞等）用簡單的英文拼讀規則猜音，效果有限但堪用。
/// </summary>
public static class EnglishArpabetIpa
{
    // ARPAbet 子音 -> IPA
    private static readonly Dictionary<string, string> ConsonantMap = new()
    {
        ["B"] = "b", ["CH"] = "ʧ", ["D"] = "d", ["DH"] = "ð", ["F"] = "f",
        ["G"] = "ɡ", ["HH"] = "h", ["JH"] = "ʤ", ["K"] = "k", ["L"] = "l",
        ["M"] = "m", ["N"] = "n", ["NG"] = "ŋ", ["P"] = "p", ["R"] = "ɹ",
        ["S"] = "s", ["SH"] = "ʃ", ["T"] = "t", ["TH"] = "θ", ["V"] = "v",
        ["W"] = "w", ["Y"] = "j", ["Z"] = "z", ["ZH"] = "ʒ",
    };

    // ARPAbet 母音 (不含重音數字) -> IPA
    private static readonly Dictionary<string, string> VowelMap = new()
    {
        ["AA"] = "ɑ", ["AE"] = "æ", ["AH"] = "ʌ", ["AO"] = "ɔ",
        ["AW"] = "aʊ", ["AY"] = "aɪ", ["EH"] = "ɛ", ["ER"] = "ɜɹ",
        ["EY"] = "eɪ", ["IH"] = "ɪ", ["IY"] = "i", ["OW"] = "oʊ",
        ["OY"] = "ɔɪ", ["UH"] = "ʊ", ["UW"] = "u",
    };

    // 重音數字 -> Kokoro 使用的重音符號（0 = 無重音，不加符號）
    private static readonly Dictionary<char, string> StressMap = new()
    {
        ['1'] = "ˈ",
        ['2'] = "ˌ",
        ['0'] = "",
    };

    private static Dictionary<string, string[]>? _dictionary;

    private static Dictionary<string, string[]> Dictionary_ => _dictionary ??= LoadDictionary();

    private static Dictionary<string, string[]> LoadDictionary()
    {
        var dict = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "ConsoleApp_TTS.cmudict.txt";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            // 找不到內建字典就回傳空字典，後續一律走拼讀猜測規則
            return dict;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            int tab = line.IndexOf('\t');
            if (tab < 0)
            {
                continue;
            }

            string word = line[..tab];
            string[] phones = line[(tab + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            dict.TryAdd(word, phones);
        }

        return dict;
    }

    /// <summary>把一個英文單字轉成 IPA 音素字串；查不到就用簡易拼讀規則猜測。</summary>
    public static string WordToIpa(string word)
    {
        string lower = word.ToLowerInvariant();
        if (Dictionary_.TryGetValue(lower, out var phones))
        {
            return ArpabetToIpa(phones);
        }

        return GuessIpa(lower);
    }

    private static string ArpabetToIpa(string[] phones)
    {
        var sb = new StringBuilder();
        foreach (var phone in phones)
        {
            // 母音音素結尾會帶重音數字，例如 "AH0"、"EY1"
            char last = phone[^1];
            if (char.IsDigit(last))
            {
                string basePhone = phone[..^1];
                string stress = StressMap.TryGetValue(last, out var s) ? s : "";
                if (VowelMap.TryGetValue(basePhone, out var vowelIpa))
                {
                    sb.Append(stress).Append(vowelIpa);
                }
                else
                {
                    sb.Append(stress).Append(basePhone.ToLowerInvariant());
                }
            }
            else if (ConsonantMap.TryGetValue(phone, out var consonantIpa))
            {
                sb.Append(consonantIpa);
            }
            else
            {
                sb.Append(phone.ToLowerInvariant());
            }
        }
        return sb.ToString();
    }

    // 查不到字典的生字：非常粗略的英文拼讀猜測（字母直接對應常見發音），
    // 準確度有限，主要避免完全無聲或亂碼，建議盡量靠字典覆蓋常用字。
    private static readonly Dictionary<char, string> FallbackLetterMap = new()
    {
        ['a'] = "æ", ['b'] = "b", ['c'] = "k", ['d'] = "d", ['e'] = "ɛ",
        ['f'] = "f", ['g'] = "ɡ", ['h'] = "h", ['i'] = "ɪ", ['j'] = "ʤ",
        ['k'] = "k", ['l'] = "l", ['m'] = "m", ['n'] = "n", ['o'] = "ɑ",
        ['p'] = "p", ['q'] = "k", ['r'] = "ɹ", ['s'] = "s", ['t'] = "t",
        ['u'] = "ʌ", ['v'] = "v", ['w'] = "w", ['x'] = "k s", ['y'] = "j",
        ['z'] = "z",
    };

    private static string GuessIpa(string lower)
    {
        var sb = new StringBuilder();
        foreach (var ch in lower)
        {
            if (FallbackLetterMap.TryGetValue(ch, out var ipa))
            {
                sb.Append(ipa);
            }
        }
        return sb.ToString();
    }
}
