using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Moq;
using Opener.Commands;
using Opener.Models;
using Opener.Services;
using Spectre.Console.Testing;
using Xunit;

namespace Opener.Tests;

[Collection("ConsoleTests")]
public class CommandIntegrationTests
{
    private readonly Mock<IConfigService> _configMock = new();
    private readonly Mock<IStorageService> _storageMock = new();
    private readonly Mock<IActionService> _actionMock = new();
    private readonly Mock<ICredentialService> _credentialMock = new();
    private readonly Mock<IGraphAuthService> _graphAuthMock = new();
    private readonly TestConsole _testConsole = new();

    private Parser CreateParser()
    {
        var root = new OpenerRootCommand(
            _configMock.Object,
            _storageMock.Object,
            _actionMock.Object,
            _credentialMock.Object,
            _graphAuthMock.Object,
            _testConsole
        );
        return new CommandLineBuilder(root).UseDefaults().Build();
    }

    [Fact]
    public async Task AddCommand_ValidInput_AddsKeySuccessfully()
    {
        _storageMock.Setup(s => s.GetKeys()).Returns(new List<OKey>());
        var parser = CreateParser();

        var exitCode = await parser.InvokeAsync("add testkey testvalue -t WebPath -e");

        Assert.Equal(0, exitCode);
        _storageMock.Verify(s => s.SaveKeys(It.Is<List<OKey>>(list => 
            list.Count == 1 &&
            list[0].Key == "testkey" &&
            list[0].Value == "testvalue" &&
            list[0].KeyType == OKeyType.WebPath &&
            list[0].Elevated
        )), Times.Once);
    }

    [Fact]
    public async Task AddCommand_JsonTypeWithFilePath_ResolvesValueFromFile()
    {
        _storageMock.Setup(s => s.GetKeys()).Returns(new List<OKey>());
        var parser = CreateParser();

        var tempFile = Path.Combine(Path.GetTempPath(), "test-template-" + Guid.NewGuid().ToString("N") + ".json");
        var jsonContent = "{\"to\":\"user@example.com\"}";
        File.WriteAllText(tempFile, jsonContent);

        try
        {
            var exitCode = await parser.InvokeAsync($"add myemail \"{tempFile.Replace("\\", "\\\\")}\" -t EmailTemplate");

            Assert.Equal(0, exitCode);
            _storageMock.Verify(s => s.SaveKeys(It.Is<List<OKey>>(list =>
                list.Count == 1 &&
                list[0].Key == "myemail" &&
                list[0].Value == jsonContent &&
                list[0].KeyType == OKeyType.EmailTemplate
            )), Times.Once);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task AddCommand_DuplicateKey_DoesNotAddKey()
    {
        _storageMock.Setup(s => s.GetKeys()).Returns(new List<OKey>
        {
            new OKey { Key = "existing", Value = "val", KeyType = OKeyType.Data }
        });
        var parser = CreateParser();

        var exitCode = await parser.InvokeAsync("add existing newval -t Data");
        Assert.Equal(0, exitCode);
        Assert.Contains("already exists", _testConsole.Output);
        _storageMock.Verify(s => s.SaveKeys(It.IsAny<List<OKey>>()), Times.Never);
    }

    [Fact]
    public async Task UpdateCommand_ExistingKey_UpdatesValue()
    {
        var existing = new OKey { Key = "existing", Value = "oldval", KeyType = OKeyType.Data };
        _storageMock.Setup(s => s.GetKeys()).Returns(new List<OKey> { existing });
        var parser = CreateParser();

        var exitCode = await parser.InvokeAsync("update existing newval -e");

        Assert.Equal(0, exitCode);
        Assert.Equal("newval", existing.Value);
        Assert.True(existing.Elevated);
        _storageMock.Verify(s => s.SaveKeys(It.IsAny<List<OKey>>()), Times.Once);
    }

    [Fact]
    public async Task UpdateCommand_MissingKey_ShowsError()
    {
        _storageMock.Setup(s => s.GetKeys()).Returns(new List<OKey>());
        var parser = CreateParser();

        var exitCode = await parser.InvokeAsync("update missing newval");
        Assert.Equal(0, exitCode);
        Assert.Contains("not found", _testConsole.Output);
        _storageMock.Verify(s => s.SaveKeys(It.IsAny<List<OKey>>()), Times.Never);
    }

    [Fact]
    public async Task DeleteCommand_SkipConfirmation_DeletesKey()
    {
        var existing = new OKey { Key = "existing", Value = "val", KeyType = OKeyType.Data };
        _storageMock.Setup(s => s.GetKeys()).Returns(new List<OKey> { existing });
        var parser = CreateParser();

        var exitCode = await parser.InvokeAsync("delete existing -y");

        Assert.Equal(0, exitCode);
        _storageMock.Verify(s => s.SaveKeys(It.Is<List<OKey>>(list => list.Count == 0)), Times.Once);
    }

    [Fact]
    public async Task DeleteCommand_CancelConfirmation_DoesNotDeleteKey()
    {
        var existing = new OKey { Key = "existing", Value = "val", KeyType = OKeyType.Data };
        _storageMock.Setup(s => s.GetKeys()).Returns(new List<OKey> { existing });
        
        _testConsole.Input.PushKey(ConsoleKey.N);
        _testConsole.Input.PushKey(ConsoleKey.Enter);
        
        var parser = CreateParser();

        var exitCode = await parser.InvokeAsync("delete existing");
        
        Assert.Equal(0, exitCode);
        Assert.Contains("Cancelled", _testConsole.Output);
        _storageMock.Verify(s => s.SaveKeys(It.IsAny<List<OKey>>()), Times.Never);
    }

    [Fact]
    public async Task ListCommand_RendersTable_AndAppliesSearchFilter()
    {
        var list = new List<OKey>
        {
            new OKey { Key = "apple", Value = "val1", KeyType = OKeyType.Data, Description = "fruit" },
            new OKey { Key = "banana", Value = "val2", KeyType = OKeyType.WebPath, Description = "yellow fruit" }
        };
        _storageMock.Setup(s => s.GetKeys()).Returns(list);
        var parser = CreateParser();

        var exitCode = await parser.InvokeAsync("list -s banana");
        
        Assert.Equal(0, exitCode);
        var output = _testConsole.Output;
        Assert.Contains("banana", output);
        Assert.DoesNotContain("apple", output);
    }

    [Fact]
    public async Task BackupCommand_WithoutPassword_CopiesDataFile()
    {
        var list = new List<OKey> { new OKey { Key = "k", Value = "v" } };
        _storageMock.Setup(s => s.GetKeys()).Returns(list);
        
        var tempFile = Path.Combine(Path.GetTempPath(), "opener-" + Guid.NewGuid().ToString("N") + ".dat");
        File.WriteAllText(tempFile, "mock-encrypted-content");
        _configMock.Setup(c => c.GetDataFilePath()).Returns(tempFile);

        var parser = CreateParser();

        try
        {
            var exitCode = await parser.InvokeAsync("backup");
            Assert.Equal(0, exitCode);
            
            var backupDir = Path.Combine(Path.GetDirectoryName(tempFile)!, ".backup");
            Assert.True(Directory.Exists(backupDir));
            var files = Directory.GetFiles(backupDir, "opener_backup_*.dat");
            Assert.NotEmpty(files);
            
            // cleanup
            Directory.Delete(backupDir, true);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExportAndImportCommands_WorkWithPortableEncryption()
    {
        var keys = new List<OKey>
        {
            new OKey { Key = "k1", Value = "v1", KeyType = OKeyType.Data },
            new OKey { Key = "k2", Value = "v2", KeyType = OKeyType.WebPath }
        };
        _storageMock.Setup(s => s.GetKeys()).Returns(keys);

        var tempFile = Path.Combine(Path.GetTempPath(), "export-" + Guid.NewGuid().ToString("N") + ".dat");
        var parser = CreateParser();

        try
        {
            // 1. Export
            var exportExitCode = await parser.InvokeAsync($"export {tempFile} -p mypassword");
            Assert.Equal(0, exportExitCode);
            Assert.True(File.Exists(tempFile));

            // 2. Import
            var currentKeys = new List<OKey>
            {
                new OKey { Key = "k1", Value = "oldval", KeyType = OKeyType.Data }
            };
            _storageMock.Setup(s => s.GetKeys()).Returns(currentKeys);

            var importExitCode = await parser.InvokeAsync($"import {tempFile} -p mypassword");
            Assert.Equal(0, importExitCode);

            _storageMock.Verify(s => s.SaveKeys(It.Is<List<OKey>>(l => 
                l.Count == 2 &&
                l.First(x => x.Key == "k1").Value == "v1" &&
                l.First(x => x.Key == "k2").Value == "v2"
            )), Times.Once);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ConfigShow_PrintsDetails()
    {
        var conf = new OpenerConfig();
        _configMock.Setup(c => c.GetConfig()).Returns(conf);
        _configMock.Setup(c => c.GetDataFilePath()).Returns("C:\\some\\path.dat");

        var parser = CreateParser();

        var exitCode = await parser.InvokeAsync("config show");
        Assert.Equal(0, exitCode);
        Assert.Contains("Storage Location", _testConsole.Output);
    }

    [Fact]
    public async Task ConfigSetLocation_ValidatesAndSavesPath()
    {
        var conf = new OpenerConfig();
        _configMock.Setup(c => c.GetConfig()).Returns(conf);

        var tempDir = Path.Combine(Path.GetTempPath(), "openerdir-" + Guid.NewGuid().ToString("N"));
        var tempFile = Path.Combine(tempDir, "opener.dat");
        
        var parser = CreateParser();

        try
        {
            var exitCode = await parser.InvokeAsync($"config set-location {tempFile}");
            Assert.Equal(0, exitCode);
            _configMock.Verify(c => c.SaveConfig(It.Is<OpenerConfig>(co => co.StorageLocation == tempFile)), Times.Once);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ConfigSetUrlAliases_ValidPairs_SavesConfig()
    {
        var conf = new OpenerConfig();
        _configMock.Setup(c => c.GetConfig()).Returns(conf);
        var parser = CreateParser();

        var exitCode = await parser.InvokeAsync("config set-url-aliases env d=-dev u=-uat p=");

        Assert.Equal(0, exitCode);
        Assert.True(conf.GlobalUrlAliases.ContainsKey("env"));
        Assert.Equal("-dev", conf.GlobalUrlAliases["env"]["d"]);
        _configMock.Verify(c => c.SaveConfig(conf), Times.Once);
    }

    [Fact]
    public async Task ConfigClearUrlAlias_RemovesAlias()
    {
        var conf = new OpenerConfig();
        conf.GlobalUrlAliases["env"] = new Dictionary<string, string> { { "d", "-dev" } };
        _configMock.Setup(c => c.GetConfig()).Returns(conf);
        var parser = CreateParser();

        var exitCode = await parser.InvokeAsync("config clear-url-alias env");

        Assert.Equal(0, exitCode);
        Assert.False(conf.GlobalUrlAliases.ContainsKey("env"));
        _configMock.Verify(c => c.SaveConfig(conf), Times.Once);
    }

    [Fact]
    public async Task ConfigSetDefaultParams_ValidInputs_SavesConfig()
    {
        var conf = new OpenerConfig();
        _configMock.Setup(c => c.GetConfig()).Returns(conf);
        var parser = CreateParser();

        var exitCode = await parser.InvokeAsync("config set-default-params user krishna");

        Assert.Equal(0, exitCode);
        Assert.Equal("krishna", conf.GlobalDefaultParams["user"]);
        _configMock.Verify(c => c.SaveConfig(conf), Times.Once);
    }

    [Fact]
    public async Task ConfigClearDefaultParam_RemovesParam()
    {
        var conf = new OpenerConfig();
        conf.GlobalDefaultParams["user"] = "krishna";
        _configMock.Setup(c => c.GetConfig()).Returns(conf);
        var parser = CreateParser();

        var exitCode = await parser.InvokeAsync("config clear-default-param user");

        Assert.Equal(0, exitCode);
        Assert.False(conf.GlobalDefaultParams.ContainsKey("user"));
        _configMock.Verify(c => c.SaveConfig(conf), Times.Once);
    }

    [Fact]
    public async Task SetProviderSmtp_ConfiguresKeys()
    {
        var keys = new List<OKey>();
        _storageMock.Setup(s => s.GetKeys()).Returns(keys);
        var parser = CreateParser();

        var exitCode = await parser.InvokeAsync("config set-provider smtp --server smtp.gmail.com --port 587 --ssl --username user@gmail.com --password mypass");

        Assert.Equal(0, exitCode);
        _storageMock.Verify(s => s.SaveKeys(It.Is<List<OKey>>(l => 
            l.Any(x => x.Key == "__provider_smtp_server" && x.Value == "smtp.gmail.com") &&
            l.Any(x => x.Key == "__provider_smtp_port" && x.Value == "587") &&
            l.Any(x => x.Key == "__provider_smtp_ssl" && x.Value == "True") &&
            l.Any(x => x.Key == "__provider_smtp_username" && x.Value == "user@gmail.com") &&
            l.Any(x => x.Key == "__provider_smtp_password" && x.Value == "mypass")
        )), Times.Once);
    }

    [Fact]
    public async Task SetProviderGraph_ValidatesAndConfiguresKeys()
    {
        var keys = new List<OKey>
        {
            new OKey { Key = "__provider_graph_refresh_token", Value = "refreshtok" }
        };
        _storageMock.Setup(s => s.GetKeys()).Returns(keys);
        _graphAuthMock.Setup(g => g.ValidateClientCredentialsAsync("tenant", "client", "secret")).ReturnsAsync(true);
        var parser = CreateParser();

        var exitCode = await parser.InvokeAsync("config set-provider graph --tenant-id tenant --client-id client --client-secret secret");

        Assert.Equal(0, exitCode);
        _storageMock.Verify(s => s.SaveKeys(It.Is<List<OKey>>(l => 
            l.Any(x => x.Key == "__provider_graph_tenant_id" && x.Value == "tenant") &&
            l.Any(x => x.Key == "__provider_graph_client_id" && x.Value == "client") &&
            l.Any(x => x.Key == "__provider_graph_client_secret" && x.Value == "secret") &&
            !l.Any(x => x.Key == "__provider_graph_refresh_token") // Device token cleaned up
        )), Times.Once);
    }

    [Fact]
    public async Task ImplicitKeyExecution_DelegatesToActionService()
    {
        var keys = new List<OKey>
        {
            new OKey { Key = "mykey", Value = "myval", KeyType = OKeyType.Data }
        };
        _storageMock.Setup(s => s.GetKeys()).Returns(keys);
        var parser = CreateParser();

        var exitCode = await parser.InvokeAsync("mykey arg1 arg2 -r -e");

        Assert.Equal(0, exitCode);
        _actionMock.Verify(a => a.ExecuteAsync(
            It.Is<OKey>(x => x.Key == "mykey"),
            It.Is<string[]>(args => args.Length == 2 && args[0] == "arg1" && args[1] == "arg2"),
            true, // returnValue
            false, // forceCopy
            true // elevated
        ), Times.Once);
    }

    [Fact]
    public async Task ViewCommand_ExistingKey_DisplaysDetails()
    {
        var keys = new List<OKey>
        {
            new OKey { Key = "mykey", Value = "myval", KeyType = OKeyType.Data }
        };
        _storageMock.Setup(s => s.GetKeys()).Returns(keys);
        var parser = CreateParser();

        var exitCode = await parser.InvokeAsync("view mykey");

        Assert.Equal(0, exitCode);
        Assert.Contains("mykey", _testConsole.Output);
        Assert.Contains("Data", _testConsole.Output);
        Assert.Contains("myval", _testConsole.Output);
    }

    [Fact]
    public async Task ImplicitKeyExecution_ViewOption_DisplaysDetails()
    {
        var keys = new List<OKey>
        {
            new OKey { Key = "mykey", Value = "{\"foo\":\"bar\"}", KeyType = OKeyType.JsonData }
        };
        _storageMock.Setup(s => s.GetKeys()).Returns(keys);
        var parser = CreateParser();

        var exitCode = await parser.InvokeAsync("mykey -v");

        Assert.Equal(0, exitCode);
        Assert.Contains("mykey", _testConsole.Output);
        Assert.Contains("JsonData", _testConsole.Output);
        Assert.Contains("foo", _testConsole.Output);
        _actionMock.Verify(a => a.ExecuteAsync(It.IsAny<OKey>(), It.IsAny<string[]>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task DocsCommand_GeneratesAndOpensDocs()
    {
        var parser = CreateParser();
        var exitCode = await parser.InvokeAsync("docs");

        Assert.Equal(0, exitCode);
        Assert.Contains("Generating documentation", _testConsole.Output);
    }
}
