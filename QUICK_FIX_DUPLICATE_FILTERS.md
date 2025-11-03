# ?? 스크린샷 문제 해결: 중복 필터 제거

## 문제 확인

스크린샷에서 보이는 것처럼 **중복된 필터**가 나타나고 있습니다:

```
?? DATA  Business: [선택하세요] ??
?? DATA  Business: [선택하세요] ??  Content: [____] ??
```

두 개의 "?? DATA Business:" 필터가 보입니다!

## 원인

탭 모드로 전환했을 때:
1. **원본 GroupBox** (DataGroupBox_FourPanel)가 탭에 이동됨 ?
2. 하지만 **XAML에 정의된 _Tab용 필터**도 여전히 존재함 ?

결과: 두 개의 필터가 겹쳐서 표시됨

## 해결 방법

### XAML에서 _Tab 필터 제거

`MainWindow.xaml` 파일을 열고 다음 섹션을 찾으세요:

**현재 상태 (약 730-840 라인):**
```xaml
<!-- 4개의 탭 DataGrid Area -->
<TabControl Grid.Row="1" Name="DataGridTabPanel" Visibility="Collapsed" Margin="2">
    <TabItem Header="DATA">
        <GroupBox Margin="5">
            <GroupBox.Header>
                <StackPanel Orientation="Horizontal">
                    <TextBlock Text="?? DATA" .../> 
                    <TextBlock Text="Business:" .../>
                    <ComboBox Name="DataBusinessFilterComboBox_Tab" .../>  <!-- ? 이거 제거 -->
                    <Button Name="ClearDataFilterButton_Tab" .../>        <!-- ? 이거 제거 -->
                    <!-- 다른 중복 컨트롤들... -->
                </StackPanel>
            </GroupBox.Header>
            <ContentControl Name="DataGridHost_DATA_Tab" />
        </GroupBox>
    </TabItem>
    <!-- EVENT, DEBUG, EXCEPTION도 마찬가지... -->
</TabControl>
```

**변경 후:**
```xaml
<!-- 4개의 탭 DataGrid Area -->
<TabControl Grid.Row="1" Name="DataGridTabPanel" Visibility="Collapsed" Margin="2">
    <TabItem Header="DATA">
        <ContentControl Name="DataGridHost_DATA_Tab" />  <!-- ? 필터 없이 단순하게 -->
    </TabItem>
    <TabItem Header="EVENT">
        <ContentControl Name="DataGridHost_EVENT_Tab" />
    </TabItem>
    <TabItem Header="DEBUG">
        <ContentControl Name="DataGridHost_DEBUG_Tab" />
    </TabItem>
    <TabItem Header="EXCEPTION">
        <ContentControl Name="DataGridHost_EXCEPTION_Tab" />
    </TabItem>
</TabControl>
```

## 단계별 수정

### Step 1: DATA Tab 찾기

**찾을 내용** (약 733라인):
```xaml
<TabItem Header="DATA">
    <GroupBox Margin="5">
        <GroupBox.Header>
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="?? DATA" .../>
                <TextBlock Text="Business:" .../>
                <ComboBox Name="DataBusinessFilterComboBox_Tab" .../>
                <Button Name="ClearDataFilterButton_Tab" .../>
            </StackPanel>
        </GroupBox.Header>
        <ContentControl Name="DataGridHost_DATA_Tab" />
    </GroupBox>
</TabItem>
```

**교체할 내용**:
```xaml
<TabItem Header="DATA">
    <ContentControl Name="DataGridHost_DATA_Tab" />
</TabItem>
```

### Step 2: EVENT Tab 수정

**찾을 내용** (약 756라인):
```xaml
<TabItem Header="EVENT">
    <GroupBox Margin="5">
        <GroupBox.Header>
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="?? EVENT" .../>
                <TextBlock Text="MsgId:" .../>
                <ComboBox Name="EventMsgIdFilterComboBox_Tab" .../>
                <Button Name="ClearEventFilterButton_Tab" .../>
            </StackPanel>
        </GroupBox.Header>
        <ContentControl Name="DataGridHost_EVENT_Tab" />
    </GroupBox>
</TabItem>
```

**교체할 내용**:
```xaml
<TabItem Header="EVENT">
    <ContentControl Name="DataGridHost_EVENT_Tab" />
</TabItem>
```

### Step 3: DEBUG Tab 수정

**찾을 내용** (약 779라인):
```xaml
<TabItem Header="DEBUG">
    <GroupBox Margin="5">
        <GroupBox.Header>
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="?? DEBUG" .../>
                <TextBlock Text="Content:" .../>
                <TextBox Name="DebugContentFilterTextBox_Tab" .../>
                <Button Name="ClearDebugFilterButton_Tab" .../>
            </StackPanel>
        </GroupBox.Header>
        <ContentControl Name="DataGridHost_DEBUG_Tab" />
    </GroupBox>
</TabItem>
```

**교체할 내용**:
```xaml
<TabItem Header="DEBUG">
    <ContentControl Name="DataGridHost_DEBUG_Tab" />
</TabItem>
```

### Step 4: EXCEPTION Tab 수정

**찾을 내용** (약 802라인):
```xaml
<TabItem Header="EXCEPTION">
    <GroupBox Margin="5">
        <GroupBox.Header>
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="?? EXCEPTION" .../>
                <TextBlock Text="Business:" .../>
                <ComboBox Name="ExceptionBusinessFilterComboBox_Tab" .../>
                <Button Name="ClearExceptionFilterButton_Tab" .../>
                <TextBlock Text="Content:" .../>
                <TextBox Name="ExceptionContentFilterTextBox_Tab" .../>
                <Button Name="ClearExceptionContentFilterButton_Tab" .../>
            </StackPanel>
        </GroupBox.Header>
        <ContentControl Name="DataGridHost_EXCEPTION_Tab" />
    </GroupBox>
</TabItem>
```

**교체할 내용**:
```xaml
<TabItem Header="EXCEPTION">
    <ContentControl Name="DataGridHost_EXCEPTION_Tab" />
</TabItem>
```

## 수정 후 예상 결과

### Before (현재 - 중복)
```
탭 모드:
┌─────────────────────────────────────────┐
│ ?? DATA  Business: [선택하세요] ??      │  ← Tab의 중복 필터
│ ?? DATA  Business: [선택하세요] ??      │  ← GroupBox의 원본 필터
│    Content: [____] ??                   │
├─────────────────────────────────────────┤
│ [DataGrid 내용]                         │
└─────────────────────────────────────────┘
```

### After (수정 후 - 단일)
```
탭 모드:
┌─────────────────────────────────────────┐
│ ?? DATA  Business: [선택하세요] ??      │  ← GroupBox의 원본 필터만
│    Content: [____] ??                   │
├─────────────────────────────────────────┤
│ [DataGrid 내용]                         │
└─────────────────────────────────────────┘
```

## 빠른 수정 팁

Visual Studio에서:

1. `Ctrl+H` (찾기 및 바꾸기)
2. **정규식 사용** 체크
3. **찾을 내용**: 
   ```
   <TabItem Header="DATA">.*?</TabItem>
   ```
4. **바꿀 내용**:
   ```xml
   <TabItem Header="DATA">
       <ContentControl Name="DataGridHost_DATA_Tab" />
   </TabItem>
   ```
5. 다른 탭들도 반복

## 확인 방법

1. 수정 후 빌드: `Ctrl+Shift+B`
2. 실행: `F5`
3. "탭으로 전환" 버튼 클릭
4. **결과 확인**:
   - ? 필터가 한 개만 보임
   - ? 중복 없음
   - ? 필터 기능 정상 작동

## 소요 시간

- **예상 시간**: 5-10분
- **난이도**: ?? (쉬움)

필터 중복 문제를 해결하면 깔끔한 UI가 완성됩니다! ??
