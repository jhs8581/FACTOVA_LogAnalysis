# 인코딩 문제 최종 해결 가이드

## 🔴 현재 상황
ComboBox, DataGrid 헤더 등 UI 요소의 한글이 여전히 깨짐

## ✅ 해결 방법

### 1단계: Visual Studio에서 모든 파일 UTF-8로 재저장

#### 방법 A: 파일 하나씩 저장
1. Visual Studio에서 파일 열기
2. **File → Advanced Save Options**
3. **Encoding:** UTF-8 with signature (Codepage 65001)
4. **Line endings:** Windows (CR LF)
5. **OK** 클릭 후 저장

#### 방법 B: PowerShell 스크립트로 일괄 변환
```powershell
# 모든 .cs, .xaml 파일을 UTF-8 BOM으로 변환
Get-ChildItem -Path . -Include *.cs,*.xaml -Recurse | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    [System.IO.File]::WriteAllText($_.FullName, $content, [System.Text.Encoding]::UTF8)
    Write-Host "Converted: $($_.Name)"
}
```

### 2단계: 특정 파일 확인 필요

다음 파일들에 한글이 하드코딩되어 있을 가능성:
- `MainWindow.xaml` - ComboBox의 기본 아이템
- `Services/DataGridManager.cs` - 필터 아이템 생성
- `Models/FilterItem.cs` - 필터 모델

### 3단계: 애플리케이션 재시작 후 테스트

### 4단계: 여전히 안되면...

**마지막 수단: 소스 코드에서 한글 제거**

한글 텍스트를 리소스 파일(.resx)로 분리:
1. Properties/Resources.resx 생성
2. 모든 한글 문자열을 리소스로 이동
3. 코드에서 `Resources.StringName` 형태로 참조

## 🎯 테스트 방법

### 테스트 1: 소스 파일 인코딩 확인
```powershell
# 파일의 인코딩 확인
$file = Get-Content "MainWindow.xaml" -Encoding Byte
if ($file[0] -eq 0xEF -and $file[1] -eq 0xBB -and $file[2] -eq 0xBF) {
    "UTF-8 BOM ✅"
} else {
    "Other encoding ❌"
}
```

### 테스트 2: 런타임 인코딩 확인
App.xaml.cs에 임시 코드 추가:
```csharp
protected override void OnStartup(StartupEventArgs e)
{
    // 콘솔 창 표시 (디버그용)
    AllocConsole();
    Console.WriteLine($"기본 인코딩: {Encoding.Default.EncodingName}");
    Console.WriteLine($"콘솔 인코딩: {Console.OutputEncoding.EncodingName}");
    Console.WriteLine("테스트 한글: 안녕하세요");
    
    base.OnStartup(e);
}

[System.Runtime.InteropServices.DllImport("kernel32.dll")]
private static extern bool AllocConsole();
```

## 📋 체크리스트

- [ ] App.xaml에 폰트 설정 추가됨
- [ ] app.manifest에 UTF-8 activeCodePage 설정됨
- [ ] App.xaml.cs에 Encoding.RegisterProvider 추가됨
- [ ] 모든 .cs 파일 UTF-8 BOM으로 저장됨
- [ ] 모든 .xaml 파일 UTF-8 BOM으로 저장됨
- [ ] 빌드 성공
- [ ] 디버그 모드에서 한글 정상 표시
- [ ] 릴리즈 빌드에서 한글 정상 표시

## 🆘 최후의 수단

그래도 안되면 프로젝트를 **새로 생성**하고 파일을 하나씩 복사:
1. 새 WPF .NET 8 프로젝트 생성
2. Properties → Character Set → Use Multi-Byte Character Set 확인
3. 파일을 하나씩 추가하면서 인코딩 확인
