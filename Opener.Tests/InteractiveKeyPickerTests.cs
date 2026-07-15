using System;
using System.Collections.Generic;
using Opener.Commands;
using Opener.Models;
using Spectre.Console.Testing;
using Xunit;

namespace Opener.Tests;

[Collection("ConsoleTests")]
public class InteractiveKeyPickerTests
{
    [Fact]
    public void Pick_TypingASearchQuery_DoesNotThrow()
    {
        // Regression test: Spectre.Console's SelectionPrompt search/highlight rendering
        // re-parses each choice's converted text as markup. Every choice includes a
        // "(KeyType)" tag, and previously this was rendered via Markup.Escape("[KeyType]"),
        // which broke as soon as a search was active - "Encountered unescaped ']' token".
        var console = new TestConsole();
        console.Interactive();
        console.Input.PushText("data");
        console.Input.PushKey(ConsoleKey.Enter);

        var keys = new List<OKey>
        {
            new OKey { Key = "anthropickey", KeyType = OKeyType.Data },
            new OKey { Key = "azurepat", KeyType = OKeyType.Data },
        };

        var selected = InteractiveKeyPicker.Pick(console, keys, "Select a key to run");

        Assert.NotNull(selected);
    }

    [Fact]
    public void Pick_DescriptionContainsBrackets_SearchActive_DoesNotThrow()
    {
        var console = new TestConsole();
        console.Interactive();
        console.Input.PushText("prod");
        console.Input.PushKey(ConsoleKey.Enter);

        var keys = new List<OKey>
        {
            new OKey { Key = "deploy", KeyType = OKeyType.WebPath, Description = "backup [prod] server" },
        };

        var selected = InteractiveKeyPicker.Pick(console, keys, "Select a key to run");

        Assert.NotNull(selected);
        Assert.Equal("deploy", selected!.Key);
    }
}
