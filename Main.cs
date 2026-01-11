using STranslate.Plugin.Translate.YoudaoLLM.View;
using STranslate.Plugin.Translate.YoudaoLLM.ViewModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace STranslate.Plugin.Translate.YoudaoLLM;

public class Main : LlmTranslatePluginBase
{
    private Control? _settingUi;
    private SettingsViewModel? _viewModel;
    private Settings Settings { get; set; } = null!;
    private IPluginContext Context { get; set; } = null!;

    // 修改为有道大模型API地址
    private const string Url = "https://openapi.youdao.com/proxy/http/llm-trans";

    public override Control GetSettingUI()
    {
        _viewModel ??= new SettingsViewModel(Context, Settings);
        _settingUi ??= new SettingsView { DataContext = _viewModel };
        return _settingUi;
    }

// ... existing code ...
    /// <summary>
    ///     语言映射 - 有道大模型支持的语言
    /// </summary>
    public override string? GetSourceLanguage(LangEnum langEnum) => langEnum switch
    {
        LangEnum.Auto => "auto",
        LangEnum.ChineseSimplified => "zh-CHS",
        LangEnum.ChineseTraditional => "zh-CHT",
        LangEnum.Cantonese => "zh-CHS", // 粤语映射到简体中文
        LangEnum.English => "en",
        LangEnum.Japanese => "ja",
        LangEnum.Korean => "ko",
        LangEnum.French => "fr",
        LangEnum.Spanish => "es",
        LangEnum.Russian => "ru",
        LangEnum.German => "de",
        LangEnum.Italian => "it",
        LangEnum.Turkish => "tr",
        LangEnum.PortuguesePortugal => "pt",
        LangEnum.PortugueseBrazil => "pt",
        LangEnum.Vietnamese => "vi",
        LangEnum.Indonesian => "id",
        LangEnum.Thai => "th",
        LangEnum.Malay => "ms",
        LangEnum.Arabic => "ar",
        LangEnum.Hindi => "hi",
        LangEnum.MongolianCyrillic => "mn", // 蒙古语西里尔文
        LangEnum.MongolianTraditional => "mn", // 蒙古语传统文字
        LangEnum.Khmer => "km", // 高棉语
        LangEnum.NorwegianBokmal => "no", // 挪威语书面
        LangEnum.NorwegianNynorsk => "no", // 挪威语新挪威语
        LangEnum.Persian => "fa", // 波斯语
        LangEnum.Swedish => "sv",
        LangEnum.Polish => "pl",
        LangEnum.Dutch => "nl",
        LangEnum.Ukrainian => "uk",
        _ => "en" // 默认返回英语
    };

    /// <summary>
    ///     语言映射 - 有道大模型支持的语言
    /// </summary>
    public override string? GetTargetLanguage(LangEnum langEnum) => langEnum switch
    {
        LangEnum.Auto => "auto",
        LangEnum.ChineseSimplified => "zh-CHS",
        LangEnum.ChineseTraditional => "zh-CHT",
        LangEnum.Cantonese => "zh-CHS", // 粤语映射到简体中文
        LangEnum.English => "en",
        LangEnum.Japanese => "ja",
        LangEnum.Korean => "ko",
        LangEnum.French => "fr",
        LangEnum.Spanish => "es",
        LangEnum.Russian => "ru",
        LangEnum.German => "de",
        LangEnum.Italian => "it",
        LangEnum.Turkish => "tr",
        LangEnum.PortuguesePortugal => "pt",
        LangEnum.PortugueseBrazil => "pt",
        LangEnum.Vietnamese => "vi",
        LangEnum.Indonesian => "id",
        LangEnum.Thai => "th",
        LangEnum.Malay => "ms",
        LangEnum.Arabic => "ar",
        LangEnum.Hindi => "hi",
        LangEnum.MongolianCyrillic => "mn", // 蒙古语西里尔文
        LangEnum.MongolianTraditional => "mn", // 蒙古语传统文字
        LangEnum.Khmer => "km", // 高棉语
        LangEnum.NorwegianBokmal => "no", // 挪威语书面
        LangEnum.NorwegianNynorsk => "no", // 挪威语新挪威语
        LangEnum.Persian => "fa", // 波斯语
        LangEnum.Swedish => "sv",
        LangEnum.Polish => "pl",
        LangEnum.Dutch => "nl",
        LangEnum.Ukrainian => "uk",
        _ => "en" // 默认返回英语
    };
// ... existing code ...


    public override void Init(IPluginContext context)
    {
        Context = context;
        Settings = context.LoadSettingStorage<Settings>();
    }

    public override void Dispose() => _viewModel?.Dispose();

public override async Task TranslateAsync(TranslateRequest request, TranslateResult result, CancellationToken cancellationToken = default)
{
    if (GetSourceLanguage(request.SourceLang) is not string sourceStr)
    {
        result.Fail(Context.GetTranslation("UnsupportedSourceLang"));
        return;
    }
    if (GetTargetLanguage(request.TargetLang) is not string targetStr)
    {
        result.Fail(Context.GetTranslation("UnsupportedTargetLang"));
        return;
    }

    // 创建请求参数
    var requestData = new Dictionary<string, string>
    {
        { "i", request.Text }, // 参数名从'q'改为'i'
        { "from", sourceStr },
        { "to", targetStr },
        { "handleOption", "0" }, // 设置为0使用Pro版本(14B)，设置为3使用Lite版本(1.5B)
        { "streamType", "full" } // 使用全量返回模式
    };

    // 添加鉴权参数
    AddAuthParams(Settings.AppKey, Settings.AppSecret, requestData);

    // 使用HttpClient直接发送请求，确保Content-Type为application/x-www-form-urlencoded
    using var httpClient = new HttpClient();
    var content = new FormUrlEncodedContent(requestData);
    
    var response = await httpClient.PostAsync(Url, content, cancellationToken);
    var responseContent = await response.Content.ReadAsStringAsync();
    
    // 解析流式响应
    string fullTranslation = await ParseStreamResponse(responseContent);
    
    if (string.IsNullOrEmpty(fullTranslation))
    {
        throw new Exception($"Translation failed. Raw response: {responseContent}");
    }

    result.Success(fullTranslation);
}



// ... existing code ...
    /// <summary>
    /// 解析流式响应
    /// </summary>
    private async Task<string> ParseStreamResponse(string response)
    {
        using var reader = new StringReader(response);
        string? line;
        string fullTranslation = "";
        
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            try
            {
                // 检查是否是SSE格式的事件数据行
                if (line.StartsWith("data:"))
                {
                    // 提取data部分
                    var dataStr = line.Substring(5).Trim(); // 移除 "data:" 前缀
                    
                    // 解析JSON数据
                    var parsedData = JsonNode.Parse(dataStr);
                    
                    if (parsedData?["successful"]?.GetValue<bool>() == true)
                    {
                        // 优先使用全量翻译结果
                        if (parsedData["data"]?["transFull"]?.ToString() is string transFull)
                        {
                            fullTranslation = transFull;
                        }
                        // 如果没有全量结果，使用增量结果
                        else if (parsedData["data"]?["transIncre"]?.ToString() is string transIncre)
                        {
                            fullTranslation += transIncre;
                        }
                    }
                    else
                    {
                        // 检查错误信息
                        var errorMsg = parsedData?["message"]?.ToString() ?? "Translation failed";
                        throw new Exception(errorMsg);
                    }
                }
            }
            catch (Exception ex)
            {
                // 如果解析失败，继续处理下一行
                continue;
            }
        }
        
        return fullTranslation;
    }
// ... existing code ...



    /*
        添加鉴权相关参数 -
        appKey : 应用ID
        salt : 随机值
        curtime : 当前时间戳(秒)
        signType : 签名版本
        sign : 请求签名

        @param appKey    您的应用ID
        @param appSecret 您的应用密钥
        @param paramsMap 请求参数表
    */
    private static void AddAuthParams(string appKey, string appSecret, Dictionary<string, string> paramsMap)
    {
        var q = paramsMap.TryGetValue("i", out string? value) ? value : string.Empty; // 参数名从'q'改为'i'
        var salt = Guid.NewGuid().ToString();
        var curtime = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds() + "";
        var sign = CalculateSign(appKey, appSecret, q, salt, curtime);
        paramsMap.Add("appKey", appKey);
        paramsMap.Add("salt", salt);
        paramsMap.Add("curtime", curtime);
        paramsMap.Add("signType", "v3");
        paramsMap.Add("sign", sign);
    }

    /*
        计算鉴权签名 -
        计算方式 : sign = sha256(appKey + input(q) + salt + curtime + appSecret)

        @param appKey    您的应用ID
        @param appSecret 您的应用密钥
        @param q         请求内容
        @param salt      随机值
        @param curtime   当前时间戳(秒)
        @return 鉴权签名sign
    */
    private static string CalculateSign(string appKey, string appSecret, string q, string salt, string curtime)
    {
        var strSrc = appKey + GetInput(q) + salt + curtime + appSecret;
        return Encrypt(strSrc);
    }

    private static string Encrypt(string strSrc)
    {
        var inputBytes = Encoding.UTF8.GetBytes(strSrc);
        var hashedBytes = SHA256.HashData(inputBytes);
        return BitConverter.ToString(hashedBytes).Replace("-", "").ToUpperInvariant();
    }

    private static string GetInput(string q)
    {
        if (q == null) return "";
        var len = q.Length;
        return len <= 20 ? q : q[..10] + len + q.Substring(len - 10, 10);
    }
}