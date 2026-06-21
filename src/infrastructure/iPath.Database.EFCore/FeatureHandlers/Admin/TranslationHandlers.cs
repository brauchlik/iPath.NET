using System.Text.Json;
using iPath.Application.Features.Admin;
using iPath.Application.Localization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.Admin;

public class GetTranslationStatusHandler(
    LocalizationFileService localizationFileService,
    ILogger<GetTranslationStatusHandler> logger)
    : IRequestHandler<GetTranslationStatusQuery, Task<TranslationStatusDto>>
{
    public Task<TranslationStatusDto> Handle(GetTranslationStatusQuery request, CancellationToken ct)
    {
        var dto = new TranslationStatusDto
        {
            Locale = request.Locale
        };

        try
        {
            // Load master key list (en.json) and the target locale
            var enData = localizationFileService.GetTranslationData("en");
            var localeData = localizationFileService.GetTranslationData(request.Locale);

            // Use en.json keys as the authoritative baseline of all keys that need translation
            var allKeys = new HashSet<string>(enData?.Words?.Keys ?? Enumerable.Empty<string>());

            // Also include any locale-specific keys not yet in en.json (backward compat)
            if (localeData?.Words != null)
            {
                foreach (var key in localeData.Words.Keys)
                {
                    allKeys.Add(key);
                }
            }

            dto.TotalKeys = allKeys.Count;

            foreach (var key in allKeys)
            {
                if (localeData?.Words != null &&
                    localeData.Words.TryGetValue(key, out var value) &&
                    !string.IsNullOrWhiteSpace(value))
                {
                    // Key is fully translated in the locale
                    dto.TranslatedKeys++;
                    dto.Words[key] = value;
                }
                else
                {
                    // Key is missing or empty in the locale
                    dto.MissingKeys++;
                    dto.UntranslatedKeys.Add(key);
                    dto.Words[key] = string.Empty;
                }

                // Copy metadata if available
                if (localeData?.WordMetadata != null &&
                    localeData.WordMetadata.TryGetValue(key, out var meta))
                {
                    dto.WordMetadata[key] = meta;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting translation status for {Locale}", request.Locale);
        }

        return Task.FromResult(dto);
    }
}

public class TranslateKeysBatchHandler(
    LocalizationFileService localizationFileService,
    IChatClient chatClient,
    IConfiguration config,
    ILogger<TranslateKeysBatchHandler> logger)
    : IRequestHandler<TranslateKeysBatchCommand, Task<TranslationResultDto>>
{
    public async Task<TranslationResultDto> Handle(TranslateKeysBatchCommand request, CancellationToken ct)
    {
        var result = new TranslationResultDto
        {
            Locale = request.Locale
        };

        if (request.Keys == null || request.Keys.Count == 0)
        {
            result.IsSuccess = true;
            return result;
        }

        try
        {
            var aiSection = config.GetSection("AiSettings");
            var isEnabled = aiSection.GetValue<bool>("IsEnabled");
            if (!isEnabled)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "AI translation is disabled because AI features are not globally enabled in configuration.";
                return result;
            }

            var provider = aiSection.GetValue<string>("Provider") ?? "Ollama";
            var model = aiSection.GetValue<string>($"{provider}:TranslationModel") 
                        ?? aiSection.GetValue<string>($"{provider}:ChatModel") 
                        ?? "llama3";

            string targetLanguage;
            try
            {
                var culture = new System.Globalization.CultureInfo(request.Locale);
                targetLanguage = culture.EnglishName.Split('(')[0].Trim();
            }
            catch
            {
                targetLanguage = request.Locale;
            }

            // Build the source XML string
            var sourceBuilder = new System.Text.StringBuilder();
            sourceBuilder.Append("<source>");
            foreach (var key in request.Keys)
            {
                var escapedKey = System.Security.SecurityElement.Escape(key);
                sourceBuilder.Append($"<sn>{escapedKey}</sn>");
            }
            sourceBuilder.Append("</source>");

            var userPrompt = "Context: This is a medical tele-consultation platform (telemedicine). All phrases belong to clinical cases, diagnostics, and patient data.\n\n" +
                             "Terminology Rules (Always apply to target translation):\n" +
                             "- 'Body Site' refers to an anatomical location on a patient's body (e.g., skin topography, organ). Do NOT translate 'site' as a website, web page, or message body.\n" +
                             "- 'Task Completed' means a workflow task is successfully finished. Do NOT translate it as 'disturbed', 'interrupted', or 'failed'.\n" +
                             "- 'Register' refers to creating a new account (Sign Up). Do NOT translate it as logging in or signing in.\n" +
                             "- 'Sender reference no' means the sender's reference number. Make sure to spell 'Referenz' correctly in German.\n" +
                             "- If a phrase is an action button (e.g., 'Accept', 'Decline', 'Return'), translate it as an imperative verb or command.\n\n" +
                             $"Translate the text between <source></source> tags below into {targetLanguage}. " +
                             "Note that you only need to output the translated result; do not provide additional explanations. " +
                             "The <sn></sn> tags indicate boundaries of individual phrases; preserve these tags in the corresponding positions. " +
                             $"The output format must be: <target><sn>translation1</sn><sn>translation2</sn>...</target>\n\n" +
                             $"{sourceBuilder}";

            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.User, userPrompt)
            };

            var options = new ChatOptions
            {
                ModelId = model
            };

            logger.LogInformation("Sending batch translation request for {Count} keys in locale '{Locale}' using AI model {Model}", request.Keys.Count, request.Locale, model);
            logger.LogInformation("Requested keys for translation: {Keys}", string.Join(", ", request.Keys.Select(k => $"'{k}'")));
            
            var chatResponse = await chatClient.GetResponseAsync(messages, options, ct);
            string responseText = chatResponse.Text ?? string.Empty;

            logger.LogInformation("AI translation raw response: {Response}", responseText);

            List<string>? translationsList = null;
            Dictionary<string, string>? translationsDict = null;

            // Check if response contains XML <sn> tags
            var hasXmlTags = System.Text.RegularExpressions.Regex.IsMatch(responseText, @"<sn>.*?</sn>", System.Text.RegularExpressions.RegexOptions.Singleline);
            if (hasXmlTags)
            {
                translationsList = ParseXmlTranslations(responseText);
            }
            else
            {
                // Fallback 1: Try JSON array
                var cleanJsonText = CleanJson(responseText);
                try
                {
                    translationsList = JsonSerializer.Deserialize<List<string>>(cleanJsonText, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch
                {
                    // Fallback 2: Try JSON dictionary
                    try
                    {
                        translationsDict = JsonSerializer.Deserialize<Dictionary<string, string>>(cleanJsonText, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    }
                    catch (JsonException jsonEx)
                    {
                        logger.LogWarning(jsonEx, "Failed to deserialize AI response in both XML and JSON modes. Raw text was: {ResponseText}", responseText);
                        result.IsSuccess = false;
                        result.ErrorMessage = $"AI returned invalid response: {jsonEx.Message}. Raw response was: {responseText}";
                        return result;
                    }
                }
            }

            if (translationsList != null)
            {
                var data = localizationFileService.GetTranslationData(request.Locale);
                if (data?.Words != null)
                {
                    int updateCount = 0;
                    var successKeysList = new List<string>();
                    var emptyValueKeysList = new List<string>();

                    if (translationsList.Count != request.Keys.Count)
                    {
                        logger.LogWarning("Batch translation count mismatch ({Mode}): Requested={RequestedCount}, Received={ReceivedCount}. Discarding batch to prevent key alignment corruption.", 
                            hasXmlTags ? "XML Mode" : "JSON Array Mode", request.Keys.Count, translationsList.Count);
                        
                        result.IsSuccess = false;
                        result.ErrorMessage = $"AI translation count mismatch (Requested {request.Keys.Count}, received {translationsList.Count}). Batch discarded to prevent alignment corruption.";
                        result.FailedKeys = request.Keys;
                        return result;
                    }

                    for (int j = 0; j < Math.Min(request.Keys.Count, translationsList.Count); j++)
                    {
                        var originalKey = request.Keys[j];
                        var translation = translationsList[j]?.Trim();

                        if (!string.IsNullOrWhiteSpace(translation))
                        {
                            data.Words[originalKey] = translation;
                            data.WordMetadata[originalKey] = new TranslationMetadata
                            {
                                ModelUsed = model,
                                TranslatedAt = DateTime.UtcNow,
                                IsHumanModified = false
                            };
                            updateCount++;
                            successKeysList.Add(originalKey);
                        }
                        else
                        {
                            emptyValueKeysList.Add(originalKey);
                        }
                    }

                    string parseMode = hasXmlTags ? "XML Mode" : "JSON Array Mode";
                    logger.LogInformation("Batch translation stats ({Mode}): Requested={RequestedCount}, Received={ReceivedCount}, Matched={MatchedCount}, EmptyValues={EmptyCount}", 
                        parseMode, request.Keys.Count, translationsList.Count, successKeysList.Count, emptyValueKeysList.Count);

                    var missingRequestedKeys = request.Keys.Skip(translationsList.Count).ToList();
                    if (missingRequestedKeys.Count > 0)
                    {
                        logger.LogWarning("These requested keys were NOT returned in the response: {MissingKeys}", string.Join(", ", missingRequestedKeys.Select(k => $"'{k}'")));
                    }
                    if (emptyValueKeysList.Count > 0)
                    {
                        logger.LogWarning("AI returned empty values for these keys: {EmptyKeys}", string.Join(", ", emptyValueKeysList.Select(k => $"'{k}'")));
                    }

                    if (updateCount > 0)
                    {
                        localizationFileService.SaveTranslation(data);
                        result.TranslatedCount = updateCount;
                    }
                    
                    result.IsSuccess = true;
                    result.SuccessfulKeys = successKeysList;
                    result.FailedKeys = missingRequestedKeys.Concat(emptyValueKeysList).ToList();
                }
                else
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"Failed to load translation file for '{request.Locale}'.";
                }
            }
            else if (translationsDict != null)
            {
                var data = localizationFileService.GetTranslationData(request.Locale);
                if (data?.Words != null)
                {
                    int updateCount = 0;
                    
                    var existingKeysMap = data.Words.Keys.ToDictionary(
                        k => k.Trim(),
                        k => k,
                        StringComparer.OrdinalIgnoreCase
                    );

                    var successKeysList = new List<string>();
                    var unmatchedKeysList = new List<string>();
                    var emptyValueKeysList = new List<string>();

                    foreach (var kvp in translationsDict)
                    {
                        var keyToLookup = kvp.Key?.Trim();
                        if (string.IsNullOrEmpty(keyToLookup)) continue;

                        if (existingKeysMap.TryGetValue(keyToLookup, out var originalKey))
                        {
                            if (!string.IsNullOrWhiteSpace(kvp.Value))
                            {
                                var trimmedTranslation = kvp.Value.Trim();
                                data.Words[originalKey] = trimmedTranslation;
                                data.WordMetadata[originalKey] = new TranslationMetadata
                                {
                                    ModelUsed = model,
                                    TranslatedAt = DateTime.UtcNow,
                                    IsHumanModified = false
                                };
                                updateCount++;
                                successKeysList.Add(originalKey);
                            }
                            else
                            {
                                emptyValueKeysList.Add(originalKey);
                            }
                        }
                        else
                        {
                            unmatchedKeysList.Add(kvp.Key);
                        }
                    }

                    logger.LogInformation("Batch translation stats (JSON Dictionary Mode): Requested={RequestedCount}, Received={ReceivedCount}, Matched={MatchedCount}, Unmatched={UnmatchedCount}, EmptyValues={EmptyCount}", 
                        request.Keys.Count, translationsDict.Count, successKeysList.Count, unmatchedKeysList.Count, emptyValueKeysList.Count);

                    if (unmatchedKeysList.Count > 0)
                    {
                        logger.LogWarning("AI returned keys that did not match requested keys: {UnmatchedKeys}", string.Join(", ", unmatchedKeysList.Select(k => $"'{k}'")));
                    }
                    if (emptyValueKeysList.Count > 0)
                    {
                        logger.LogWarning("AI returned empty values for these keys: {EmptyKeys}", string.Join(", ", emptyValueKeysList.Select(k => $"'{k}'")));
                    }
                    
                    var missingRequestedKeys = request.Keys.Where(k => !successKeysList.Any(sk => string.Equals(sk, k, StringComparison.OrdinalIgnoreCase))).ToList();
                    if (missingRequestedKeys.Count > 0)
                    {
                        logger.LogWarning("These requested keys were NOT successfully translated: {MissingKeys}", string.Join(", ", missingRequestedKeys.Select(k => $"'{k}'")));
                    }

                    if (updateCount > 0)
                    {
                        localizationFileService.SaveTranslation(data);
                        result.TranslatedCount = updateCount;
                    }
                    
                    result.IsSuccess = true;
                    result.SuccessfulKeys = successKeysList;
                    result.FailedKeys = missingRequestedKeys;
                }
                else
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"Failed to load translation file for '{request.Locale}'.";
                }
            }
            else
            {
                result.IsSuccess = false;
                result.ErrorMessage = "AI returned empty results.";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing AI translation for locale '{Locale}'", request.Locale);
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private static List<string> ParseXmlTranslations(string text)
    {
        var list = new List<string>();
        if (string.IsNullOrEmpty(text)) return list;

        var matches = System.Text.RegularExpressions.Regex.Matches(text, @"<sn>(.*?)</sn>", System.Text.RegularExpressions.RegexOptions.Singleline);
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var val = match.Groups[1].Value;
            val = System.Net.WebUtility.HtmlDecode(val).Trim();
            val = StripQuotes(val);
            list.Add(val);
        }
        return list;
    }

    private static string StripQuotes(string text)
    {
        text = text.Trim();
        if (text.StartsWith("\"") && text.EndsWith("\""))
        {
            text = text.Substring(1, text.Length - 2);
        }
        else if (text.StartsWith("“") && text.EndsWith("”"))
        {
            text = text.Substring(1, text.Length - 2);
        }
        else if (text.StartsWith("„") && text.EndsWith("“"))
        {
            text = text.Substring(1, text.Length - 2);
        }
        return text.Trim();
    }

    private static string CleanJson(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring(7);
        }
        else if (text.StartsWith("```"))
        {
            text = text.Substring(3);
        }
        if (text.EndsWith("```"))
        {
            text = text.Substring(0, text.Length - 3);
        }
        return text.Trim();
    }
}

public class UpdateTranslationKeyHandler(
    LocalizationFileService localizationFileService,
    ILogger<UpdateTranslationKeyHandler> logger)
    : IRequestHandler<UpdateTranslationKeyCommand, Task<bool>>
{
    public Task<bool> Handle(UpdateTranslationKeyCommand request, CancellationToken ct)
    {
        try
        {
            var data = localizationFileService.GetTranslationData(request.Locale);
            if (data?.Words != null)
            {
                data.Words[request.Key] = request.Translation.Trim();
                data.WordMetadata[request.Key] = new TranslationMetadata
                {
                    ModelUsed = "Human",
                    TranslatedAt = DateTime.UtcNow,
                    IsHumanModified = true
                };
                bool saved = localizationFileService.SaveTranslation(data);
                return Task.FromResult(saved);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating translation key '{Key}' for locale '{Locale}'", request.Key, request.Locale);
        }
        return Task.FromResult(false);
    }
}
