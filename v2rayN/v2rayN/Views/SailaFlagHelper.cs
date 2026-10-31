using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ServiceLib.Common;

namespace v2rayN.Views;

public static class SailaFlagHelper
{
    private static readonly ConcurrentDictionary<string, ImageSource?> _flagCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    private static readonly HashSet<string> ValidIsoCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ad", "ae", "af", "ag", "ai", "al", "am", "ao", "aq", "ar", "as", "at", "au", "aw", "ax", "az",
        "ba", "bb", "bd", "be", "bf", "bg", "bh", "bi", "bj", "bl", "bm", "bn", "bo", "bq", "br", "bs",
        "bt", "bv", "bw", "by", "bz", "ca", "cc", "cd", "cf", "cg", "ch", "ci", "ck", "cl", "cm", "cn",
        "co", "cr", "cu", "cv", "cw", "cx", "cy", "cz", "de", "dj", "dk", "dm", "do", "dz", "ec", "ee",
        "eg", "eh", "er", "es", "et", "fi", "fj", "fk", "fm", "fo", "fr", "ga", "gb", "gd", "ge", "gf",
        "gg", "gh", "gi", "gl", "gm", "gn", "gp", "gq", "gr", "gs", "gt", "gu", "gw", "gy", "hk", "hm",
        "hn", "hr", "ht", "hu", "id", "ie", "il", "im", "in", "io", "iq", "ir", "is", "it", "je", "jm",
        "jo", "jp", "ke", "kg", "kh", "ki", "km", "kn", "kp", "kr", "kw", "ky", "kz", "la", "lb", "lc",
        "li", "lk", "lr", "ls", "lt", "lu", "lv", "ly", "ma", "mc", "md", "me", "mf", "mg", "mh", "mk",
        "ml", "mm", "mn", "mo", "mp", "mq", "mr", "ms", "mt", "mu", "mv", "mw", "mx", "my", "mz", "na",
        "nc", "ne", "nf", "ng", "ni", "nl", "no", "np", "nr", "nu", "nz", "om", "pa", "pe", "pf", "pg",
        "ph", "pk", "pl", "pm", "pn", "pr", "ps", "pt", "pw", "py", "qa", "re", "ro", "rs", "ru", "rw",
        "sa", "sb", "sc", "sd", "se", "sg", "sh", "si", "sj", "sk", "sl", "sm", "sn", "so", "sr", "ss",
        "st", "sv", "sx", "sy", "sz", "tc", "td", "tf", "tg", "th", "tj", "tk", "tl", "tm", "tn", "to",
        "tr", "tt", "tv", "tw", "tz", "ua", "ug", "um", "us", "uy", "uz", "va", "vc", "ve", "vg", "vi",
        "vn", "vu", "wf", "ws", "ye", "yt", "za", "zm", "zw"
    };

    private static readonly (string name, string code)[] CountryNameMap =
    [
        // Persian names
        ("ایران", "ir"),
        ("آلمان", "de"),
        ("المـان", "de"),
        ("انگلیس", "gb"),
        ("انگلستان", "gb"),
        ("بریتانیا", "gb"),
        ("امریکا", "us"),
        ("آمریکا", "us"),
        ("ایالات متحده", "us"),
        ("فرانسه", "fr"),
        ("هلند", "nl"),
        ("ترکیه", "tr"),
        ("فنلاند", "fi"),
        ("کانادا", "ca"),
        ("سوئد", "se"),
        ("سوئیس", "ch"),
        ("سوییس", "ch"),
        ("روسیه", "ru"),
        ("امارات", "ae"),
        ("دبی", "ae"),
        ("دوبی", "ae"),
        ("لهستان", "pl"),
        ("اسپانیا", "es"),
        ("ایتالیا", "it"),
        ("ژاپن", "jp"),
        ("سنگاپور", "sg"),
        ("اتریش", "at"),
        ("بلژیک", "be"),
        ("نروژ", "no"),
        ("دانمارک", "dk"),
        ("استرالیا", "au"),
        ("هند", "in"),
        ("برزیل", "br"),
        ("گرجستان", "ge"),
        ("ارمنستان", "am"),
        ("آذربایجان", "az"),
        ("عراق", "iq"),
        ("اوکراین", "ua"),

        // English names & common abbreviations
        ("germany", "de"),
        ("deutschland", "de"),
        ("france", "fr"),
        ("iran", "ir"),
        ("united states", "us"),
        ("usa", "us"),
        ("united kingdom", "gb"),
        ("britain", "gb"),
        ("england", "gb"),
        ("netherlands", "nl"),
        ("holland", "nl"),
        ("turkey", "tr"),
        ("turkiye", "tr"),
        ("türkiye", "tr"),
        ("finland", "fi"),
        ("canada", "ca"),
        ("sweden", "se"),
        ("switzerland", "ch"),
        ("russia", "ru"),
        ("uae", "ae"),
        ("dubai", "ae"),
        ("poland", "pl"),
        ("spain", "es"),
        ("italy", "it"),
        ("japan", "jp"),
        ("singapore", "sg"),
        ("austria", "at"),
        ("belgium", "be"),
        ("norway", "no"),
        ("denmark", "dk"),
        ("australia", "au"),
        ("india", "in"),
        ("brazil", "br"),
        ("georgia", "ge"),
        ("armenia", "am"),
        ("azerbaijan", "az"),
        ("iraq", "iq"),
        ("ukraine", "ua"),
        ("hong kong", "hk"),
        ("hongkong", "hk"),
        ("korea", "kr"),
        ("taiwan", "tw"),
        ("romania", "ro"),
        ("bulgaria", "bg"),
        ("czech", "cz"),
        ("hungary", "hu"),
        ("ireland", "ie"),
        ("portugal", "pt"),
        ("greece", "gr"),
        ("israel", "il"),
        ("cyprus", "cy"),
        ("estonia", "ee"),
        ("latvia", "lv"),
        ("lithuania", "lt"),
        ("luxembourg", "lu"),
        ("moldova", "md"),
        ("serbia", "rs"),
        ("slovakia", "sk"),
        ("slovenia", "si"),
        ("iceland", "is"),
    ];

    public static (string? countryCode, string initials, string cleanName) ParseName(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return (null, "SL", "Server");
        }

        string text = rawName.Trim();
        string? countryCode = null;

        // 1. Decode Flag Emoji (two Regional Indicator Symbols: U+1F1E6 to U+1F1FF)
        var codes = new List<char>();
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsSurrogatePair(text, i))
            {
                int cp = char.ConvertToUtf32(text, i);
                if (cp >= 0x1F1E6 && cp <= 0x1F1FF)
                {
                    codes.Add((char)('a' + (cp - 0x1F1E6)));
                }
                i++; // skip second surrogate char
            }
        }

        if (codes.Count >= 2)
        {
            countryCode = $"{codes[0]}{codes[1]}";
        }

        // Strip flag emoji and other regional indicator glyphs from the displayed name
        // so Windows does not show square boxes.
        var sbClean = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsSurrogatePair(text, i))
            {
                int cp = char.ConvertToUtf32(text, i);
                if (cp >= 0x1F1E6 && cp <= 0x1F1FF)
                {
                    i++; // skip flag emoji
                    continue;
                }
                sbClean.Append(text[i]);
                sbClean.Append(text[i + 1]);
                i++;
            }
            else
            {
                sbClean.Append(text[i]);
            }
        }

        string cleanName = sbClean.ToString().Trim();
        cleanName = Regex.Replace(cleanName, @"\s+", " ");
        // Strip any dangling leading separators like "- ", "| ", "_ "
        cleanName = cleanName.TrimStart('-', '|', '_', '/', ':', ' ');

        if (string.IsNullOrWhiteSpace(cleanName))
        {
            cleanName = rawName.Trim();
        }

        // 2. If no flag emoji found, check first word for 2-letter ISO code
        if (countryCode == null)
        {
            var parts = cleanName.Split(new[] { ' ', '-', '_', '|', '/', ':', '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                string first = parts[0].Trim().ToLowerInvariant();
                if (first == "uk")
                {
                    countryCode = "gb";
                }
                else if (first.Length == 2 && ValidIsoCodes.Contains(first))
                {
                    countryCode = first;
                }
            }
        }

        // 3. If still no country code, search for English/Persian country names in the name
        if (countryCode == null)
        {
            string lowerName = cleanName.ToLowerInvariant();
            foreach (var (name, code) in CountryNameMap)
            {
                if (lowerName.Contains(name, StringComparison.OrdinalIgnoreCase))
                {
                    countryCode = code;
                    break;
                }
            }
        }

        // Generate initials
        string initials = "SL";
        if (!string.IsNullOrEmpty(countryCode))
        {
            initials = countryCode.ToUpperInvariant();
        }
        else
        {
            var words = cleanName.Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2 && words[0].Length > 0 && words[1].Length > 0)
            {
                initials = $"{char.ToUpper(words[0][0])}{char.ToUpper(words[1][0])}";
            }
            else if (cleanName.Length >= 2)
            {
                initials = cleanName.Substring(0, 2).ToUpperInvariant();
            }
            else if (cleanName.Length == 1)
            {
                initials = cleanName.ToUpperInvariant();
            }
        }

        return (countryCode, initials, cleanName);
    }

    public static async Task<ImageSource?> GetFlagAsync(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Length != 2)
        {
            return null;
        }

        string code = countryCode.ToLowerInvariant();

        if (_flagCache.TryGetValue(code, out var cached))
        {
            return cached;
        }

        try
        {
            string tempDir = Path.Combine(Utils.GetTempPath(), "flags");
            Directory.CreateDirectory(tempDir);
            string filePath = Path.Combine(tempDir, $"{code}.png");

            byte[]? bytes = null;

            if (File.Exists(filePath))
            {
                try
                {
                    bytes = await File.ReadAllBytesAsync(filePath);
                }
                catch { }
            }

            if (bytes == null || bytes.Length == 0)
            {
                string url = $"https://flagcdn.com/w80/{code}.png";
                using var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    bytes = await response.Content.ReadAsByteArrayAsync();
                    try
                    {
                        await File.WriteAllBytesAsync(filePath, bytes);
                    }
                    catch { }
                }
            }

            if (bytes != null && bytes.Length > 0)
            {
                var bitmap = new BitmapImage();
                using (var ms = new MemoryStream(bytes))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                }
                bitmap.Freeze();

                _flagCache[code] = bitmap;
                return bitmap;
            }
        }
        catch
        {
        }

        _flagCache[code] = null;
        return null;
    }
}

