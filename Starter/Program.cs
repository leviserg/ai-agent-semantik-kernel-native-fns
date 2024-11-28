using System.Text;
using AITravelAgent.Plugins.ConvertCurrency;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Plugins.Core;
using Newtonsoft.Json;
#pragma warning disable SKEXP0050 
#pragma warning disable SKEXP0060

string filePath = @"../../../../../azure-open-ai-settings.json";

try
{
    string? jsonContent = File.ReadAllText(filePath);

    Dictionary<string, string> config = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent);

    if (config == null)
    {
        throw new FileNotFoundException("Error parsing config");
    }

    string apikey = config["primarykey"];
    string endpoint = config["endpoint"];
    string modelId = config["modelId"];
    string deploymentName = config["deploymentName"];

    Console.WriteLine("Configuration loaded.");

    var builder = Kernel.CreateBuilder();
    builder.Services.AddAzureOpenAIChatCompletion(
        deploymentName,
        endpoint,
        apikey,
        "gpt-35-turbo-16k"); // documentation says to leave
    var kernel = builder.Build();

    // Note: ChatHistory isn't working correctly as of SemanticKernel v 1.4.0

    #region invoke simple function
    /* 
     * ########### invoke simple function ###########
        kernel.ImportPluginFromType<CurrencyConverter>();
        kernel.ImportPluginFromType<ConversationSummaryPlugin>();

        var result = await kernel.InvokeAsync("CurrencyConverter",
            "ConvertAmount",
            new() {
            {"targetCurrencyCode", "USD"},
            {"amount", "52000"},
            {"baseCurrencyCode", "VND"}
            }
        );

        Console.WriteLine(result);
    */
    #endregion

    #region invoke with static input
    /*
     * ########### Check with static input ###########
    kernel.ImportPluginFromType<CurrencyConverter>();
    kernel.ImportPluginFromType<ConversationSummaryPlugin>();
    var prompts = kernel.ImportPluginFromPromptDirectory("Prompts");

    var result = await kernel.InvokeAsync(
        prompts["GetTargetCurrencies"],
        new() {
            {"input", "How many Australian Dollars is 140,000 Korean Won worth?"}
        }
    );

    Console.WriteLine(result);
    */
    #endregion

    #region onetime dynamic user input with Route Intent
    /*
    kernel.ImportPluginFromType<CurrencyConverter>();
    kernel.ImportPluginFromType<ConversationSummaryPlugin>();

    var prompts = kernel.ImportPluginFromPromptDirectory("Prompts");

    OpenAIPromptExecutionSettings settings = new()
    {
        ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
    };

    Console.WriteLine("What would you like to do?");
    var userInput = Console.ReadLine();

    var intent = await kernel.InvokeAsync(
        prompts["GetIntent"],
        new() { { "input", userInput } }
    );

    string? intentValue = intent.GetValue<string>();

    switch (intentValue)
    {
        case "ConvertCurrency":
            var currencyText = await kernel.InvokeAsync<string>(
                prompts["GetTargetCurrencies"],
                new() { { "input", userInput } }
            );
            var currencyInfo = currencyText!.Split("|");
            var result = await kernel.InvokeAsync("CurrencyConverter",
                "ConvertAmount",
                new() {
                {"targetCurrencyCode", currencyInfo[0]},
                {"baseCurrencyCode", currencyInfo[1]},
                {"amount", currencyInfo[2]},
                }
            );
            Console.WriteLine(result);
            break;
        case "SuggestDestinations":
        case "SuggestActivities":
        case "HelpfulPhrases":
        case "Translate":
            var autoInvokeResult = await kernel.InvokePromptAsync(userInput!, new(settings));
            Console.WriteLine(autoInvokeResult);
            break;
        default:
            Console.WriteLine("Sure, I can help with that.");
            var otherIntentResult = await kernel.InvokePromptAsync(userInput!, new(settings));
            Console.WriteLine(otherIntentResult);
            break;
    }
    */
    #endregion

    #region consequence dynamice user input

    kernel.ImportPluginFromType<CurrencyConverter>();
    kernel.ImportPluginFromType<ConversationSummaryPlugin>();

    var prompts = kernel.ImportPluginFromPromptDirectory("Prompts");

    OpenAIPromptExecutionSettings settings = new()
    {
        ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
    };

    StringBuilder chatHistory = new();
    string? userInput;

    do
    {
        Console.WriteLine("What would you like to do?");

        userInput = Console.ReadLine();

        var intent = await kernel.InvokeAsync(
            prompts["GetIntent"],
            new() { { "input", userInput } }
        );

        string? intentValue = intent.GetValue<string>();

        switch (intentValue)
        {
            case "ConvertCurrency":
                var currencyText = await kernel.InvokeAsync<string>(
                    prompts["GetTargetCurrencies"],
                    new() { { "input", userInput } }
                );
                var currencyInfo = currencyText!.Split("|");
                var result = await kernel.InvokeAsync("CurrencyConverter",
                    "ConvertAmount",
                    new() {
                {"targetCurrencyCode", currencyInfo[0]},
                {"baseCurrencyCode", currencyInfo[1]},
                {"amount", currencyInfo[2]},
                    }
                );
                Console.WriteLine(result);
                break;
            case "SuggestDestinations":
                chatHistory.AppendLine("User:" + userInput);
                var recommendations = await kernel.InvokePromptAsync(userInput!);
                Console.WriteLine(recommendations);
                break;
            case "SuggestActivities":

                var chatSummary = await kernel.InvokeAsync(
                    "ConversationSummaryPlugin",
                    "SummarizeConversation",
                    new() { { "input", chatHistory.ToString() } });

                var activities = await kernel.InvokePromptAsync(
                    userInput,
                    new() {
                        {"input", userInput},
                        {"history", chatSummary},
                        {"ToolCallBehavior", ToolCallBehavior.AutoInvokeKernelFunctions}
                });

                chatHistory.AppendLine("User:" + userInput);
                chatHistory.AppendLine("Assistant:" + activities.ToString());

                Console.WriteLine(activities);
                break;
            case "HelpfulPhrases":
            case "Translate":
                var autoInvokeResult = await kernel.InvokePromptAsync(userInput!, new(settings));
                Console.WriteLine(autoInvokeResult);
                break;
            default:
                Console.WriteLine("Sure, I can help with that.");
                var otherIntentResult = await kernel.InvokePromptAsync(userInput!, new(settings));
                Console.WriteLine(otherIntentResult);
                break;
        }
    }
    while (!string.IsNullOrWhiteSpace(userInput));

    #endregion

}
catch (FileNotFoundException ex)
{
    Console.WriteLine("No configuration file found : " + ex.Message);
}

Console.WriteLine("Press Ctrl+C to exit...");
Console.ReadKey();