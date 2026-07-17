# Chay ung dung local

Chay tu thu muc goc repo:

```powershell
dotnet run --project .\src\PDFPageComposer.App\PDFPageComposer.App.csproj
```

Neu muon build nhanh truoc khi chay:

```powershell
dotnet build .\PDFPageComposer.slnx
dotnet run --project .\src\PDFPageComposer.App\PDFPageComposer.App.csproj
```

Chay test:

```powershell
dotnet test .\PDFPageComposer.slnx
```

Chay ban da publish:

```powershell
.\src\PDFPageComposer.App\bin\Release\net10.0-windows\win-x64\publish\PDFPageComposer.App.exe
```
