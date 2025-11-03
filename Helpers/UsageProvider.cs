namespace FACTOVA_LogAnalysis.Helpers
{
    public static class UsageProvider
    {
        public static string GetUsageText()
        {
            return @"# 프로그램 사용법

이 문서는 FACTOVA Log Analysis 도구의 주요 기능과 사용 방법을 안내합니다.

## 빠른 시작

1. 상단의 `Log Folder`에서 로그가 들어있는 폴더를 설정합니다.
2. 로드 옵션에서 필요한 항목(?? Text, ?? DataGrid, ? exec.Time)을 선택합니다.
3. 날짜 및 시간 범위를 설정하고 `?? Load` 버튼을 눌러 로그를 읽어옵니다.

## 텍스트 탭 (SFC Log)

- 4분할 창으로 DATA / EVENT / DEBUG / EXCEPTION 로그를 동시에 봅니다.
- 각 패널의 왼쪽에는 행 번호, 오른쪽에는 실제 로그 내용이 표시됩니다.
- 검색: 텍스트 영역에서 우클릭 또는 툴바의 Find 버튼으로 찾기 대화창을 엽니다.
- 글꼴 크기는 상단 툴바에서 조정할 수 있습니다.

## DataGrid 탭

- 로그를 표 형식으로 보고 컬럼별 정렬/필터가 가능합니다.
- `?? Keyword` 입력 시 모든 DataGrid에 대해 글로벌 필터가 적용됩니다.
- 특정 컬럼 중 `Content`는 가변 너비로 표시되며, 필요 시 Export 기능으로 XLSX로 저장할 수 있습니다.

## Clipboard Pad

- 자주 사용하는 텍스트를 9개의 슬롯에 보관할 수 있습니다.
- 슬롯을 선택한 뒤 복사/붙여넣기 기능을 이용해 편리하게 텍스트를 삽입하세요.

## exec.Time Analysis

- 특정 시간 또는 exec.Time 임계값으로 로그를 필터링하여 성능 병목을 분석합니다.

## 색상 강조(Red Business List)

- 중요 비즈니스 이름을 등록해 해당 로그를 색상으로 하이라이트할 수 있습니다.

## 작업 로그 (Work Log)

- 내부 동작과 사용자 알림이 기록됩니다. 필요한 경우 파일로 저장할 수 있습니다.

## 검색 및 찾기

- `Find` 대화창을 통해 현재 활성 텍스트 박스 내에서 검색이 가능합니다.
- 대화창의 위/아래 버튼으로 다음/이전 결과로 이동합니다.
- Match case 옵션을 사용하여 대소문자 구분 검색이 가능합니다.

## 팁 및 문제해결

- 로그가 보이지 않으면 `Log Folder` 설정과 선택한 날짜/시간 범위를 확인하세요.
- DataGrid의 행을 선택한 뒤 `? Go To Time` 버튼을 사용하면 다른 그리드에서 유사 시점으로 빠르게 이동합니다.
- 성능 문제 발생 시 텍스트 로드 옵션을 끄고 DataGrid 모드만으로 테스트해 보세요.

## 지원

- 문제가 지속되면 Work Log 내용을 캡처하여 개발자에게 전달하면 원인 파악에 도움이 됩니다.

(끝)
";
        }
    }
}
