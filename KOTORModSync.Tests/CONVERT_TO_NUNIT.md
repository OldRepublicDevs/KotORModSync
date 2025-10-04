# Converting XUnit Tests to NUnit

The test files were initially created for XUnit, but the project uses NUnit. Here are the simple changes needed:

## Quick Conversions Needed

### 1. VirtualFileSystemWildcardTests.cs

- [ ] Replace `using Xunit;` → `using NUnit.Framework;`
- [ ] Remove `using Xunit.Abstracts;`
- [ ] Replace `public class VirtualFileSystemWildcardTests : IDisposable` → `[TestFixture] public class VirtualFileSystemWildcardTests`
- [ ] Replace constructor `public VirtualFileSystemWildcardTests(ITestOutputHelper output)` → `public VirtualFileSystemWildcardTests()`
- [ ] Remove `_output` field: `private readonly ITestOutputHelper _output;`
- [ ] Remove `_output = output;` from constructor
- [ ] Add `[TearDown]` attribute before `Dispose()` method
- [ ] Replace all `[Fact]` → `[Test]`
- [ ] Replace all `_output.WriteLine` → `TestContext.WriteLine`

### 2. DryRunValidationIntegrationTests.cs

- Same conversions as above

### 3. MainConfig Access

The tests need to set MainConfig properties but they're not accessible. You'll need to either:

- Make the setters internal/public in MainConfig
- OR create a test helper method in MainConfig like `SetForTesting(...)`
- OR use reflection to set them

## Find/Replace Commands

### PowerShell

```powershell
# In each test file
(Get-Content $file) -replace 'using Xunit;', 'using NUnit.Framework;' | Set-Content $file
(Get-Content $file) -replace 'using Xunit.Abstracts;', '' | Set-Content $file
(Get-Content $file) -replace '\[Fact\]', '[Test]' | Set-Content $file
(Get-Content $file) -replace '_output\.WriteLine', 'TestContext.WriteLine' | Set-Content $file
(Get-Content $file) -replace 'ITestOutputHelper', 'TestContext' | Set-Content $file
(Get-Content $file) -replace ': IDisposable', '' | Set-Content $file
(Get-Content $file) -replace 'public void Dispose\(\)', '[TearDown]\n\tpublic void Dispose()' | Set-Content $file
(Get-Content $file) -replace 'public class (\w+) \{', '[TestFixture]\n\tpublic class $1 {' | Set-Content $file
```

### Linux/Mac (sed)

```bash
# In each test file
sed -i 's/using Xunit;/using NUnit.Framework;/g' $file
sed -i 's/using Xunit.Abstracts;//g' $file
sed -i 's/\[Fact\]/[Test]/g' $file
sed -i 's/_output\.WriteLine/TestContext.WriteLine/g' $file
```

## Or Just Use IDE Refactoring

1. Open each test file
2. Ctrl+H (Find/Replace)
3. Apply the replacements above one by one

## Verification

After conversion, run:

```bash
dotnet build KOTORModSync.Tests
dotnet test KOTORModSync.Tests
```

All tests should compile and run successfully!
