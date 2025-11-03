# Complete Implementation Guide: Remove Duplicate Tab Controls

## Overview
This guide will help you remove all redundant `_Tab` filter controls from the XAML file. The entire GroupBox (with its filters) now moves between 4-panel and tab views.

## Step 1: Locate the Tab Section in MainWindow.xaml

Find the section that starts with:
```xaml
<!-- 4개의 탭 DataGrid Area -->
<TabControl Grid.Row="1" Name="DataGridTabPanel" Visibility="Collapsed" Margin="2">
```

This is approximately around line 730-830 in your XAML file.

## Step 2: Replace DATA Tab

**FIND** (approximately lines 733-755):
```xaml
<TabItem Header="DATA">
    <GroupBox Margin="5">
        <GroupBox.Header>
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="?? DATA" VerticalAlignment="Center" FontWeight="Bold" FontSize="12" Foreground="DarkBlue" Margin="0,0,10,0"/>
                <TextBlock Text="Business:" VerticalAlignment="Center" FontSize="14" Margin="0,0,3,0"/>
                <ComboBox Name="DataBusinessFilterComboBox_Tab" Width="200" Height="32" FontSize="14" 
                          ToolTip="Filter DATA logs by business name (Multi-select with checkboxes)" 
                          Margin="0,0,5,0" Padding="8,5" VerticalContentAlignment="Center"
                          IsEditable="True"
                          Text="선택하세요">
                </ComboBox>
                <Button Name="ClearDataFilterButton_Tab" Content="??" Width="28" Height="30" FontSize="13"
                        Click="ClearDataFilterButton_Click" ToolTip="Clear DATA filter" Margin="3,0"/>
            </StackPanel>
        </GroupBox.Header>
        <!-- Host for single shared DataGrid in tab view -->
        <ContentControl Name="DataGridHost_DATA_Tab" />
    </GroupBox>
</TabItem>
```

**REPLACE WITH**:
```xaml
<TabItem Header="DATA">
    <ContentControl Name="DataGridHost_DATA_Tab" />
</TabItem>
```

## Step 3: Replace EVENT Tab

**FIND** (approximately lines 756-778):
```xaml
<TabItem Header="EVENT">
    <GroupBox Margin="5">
        <GroupBox.Header>
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="?? EVENT" VerticalAlignment="Center" FontWeight="Bold" FontSize="12" Foreground="DarkGreen" Margin="0,0,10,0"/>
                <TextBlock Text="MsgId:" VerticalAlignment="Center" FontSize="14" Margin="0,0,3,0"/>
                <ComboBox Name="EventMsgIdFilterComboBox_Tab" Width="200" Height="32" FontSize="14" 
                          ToolTip="Filter EVENT logs by MsgId (Multi-select with checkboxes)" 
                          Margin="0,0,5,0" Padding="8,5" VerticalContentAlignment="Center"
                          IsEditable="True"
                          Text="선택하세요">
                </ComboBox>
                <Button Name="ClearEventFilterButton_Tab" Content="??" Width="28" Height="30" FontSize="13"
                        Click="ClearEventFilterButton_Click" ToolTip="Clear EVENT filter" Margin="3,0"/>
            </StackPanel>
        </GroupBox.Header>
        <ContentControl Name="DataGridHost_EVENT_Tab" />
    </GroupBox>
</TabItem>
```

**REPLACE WITH**:
```xaml
<TabItem Header="EVENT">
    <ContentControl Name="DataGridHost_EVENT_Tab" />
</TabItem>
```

## Step 4: Replace DEBUG Tab

**FIND** (approximately lines 779-801):
```xaml
<TabItem Header="DEBUG">
    <GroupBox Margin="5">
        <GroupBox.Header>
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="?? DEBUG" VerticalAlignment="Center" FontWeight="Bold" FontSize="12" Foreground="DarkOrange" Margin="0,0,10,0"/>
                <TextBlock Text="Content:" VerticalAlignment="Center" FontSize="14" Margin="0,0,3,0"/>
                <TextBox Name="DebugContentFilterTextBox_Tab" Width="200" Height="32" FontSize="14" 
                         ToolTip="Filter DEBUG logs by content keywords" 
                         TextChanged="DebugContentFilterTextBox_TextChanged" Padding="8,5" VerticalContentAlignment="Center"
                         Margin="0,0,5,0"/>
                <Button Name="ClearDebugFilterButton_Tab" Content="??" Width="28" Height="30" FontSize="13"
                        Click="ClearDebugFilterButton_Click" ToolTip="Clear DEBUG filter" Margin="3,0"/>
            </StackPanel>
        </GroupBox.Header>
        <ContentControl Name="DataGridHost_DEBUG_Tab" />
    </GroupBox>
</TabItem>
```

**REPLACE WITH**:
```xaml
<TabItem Header="DEBUG">
    <ContentControl Name="DataGridHost_DEBUG_Tab" />
</TabItem>
```

## Step 5: Replace EXCEPTION Tab

**FIND** (approximately lines 802-833):
```xaml
<TabItem Header="EXCEPTION">
    <GroupBox Margin="5">
        <GroupBox.Header>
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="?? EXCEPTION" VerticalAlignment="Center" FontWeight="Bold" FontSize="12" Foreground="DarkRed" Margin="0,0,10,0"/>
                <TextBlock Text="Business:" VerticalAlignment="Center" FontSize="14" Margin="0,0,3,0"/>
                <ComboBox Name="ExceptionBusinessFilterComboBox_Tab" Width="200" Height="32" FontSize="14" 
                          ToolTip="Filter EXCEPTION logs by business name (Multi-select with checkboxes)" 
                          Margin="0,0,5,0" Padding="8,5" VerticalContentAlignment="Center"
                          IsEditable="True"
                          Text="선택하세요">
                </ComboBox>
                <Button Name="ClearExceptionFilterButton_Tab" Content="??" Width="28" Height="30" FontSize="13"
                        Click="ClearExceptionFilterButton_Click" ToolTip="Clear EXCEPTION filter" Margin="3,0"/>
                <TextBlock Text="Content:" VerticalAlignment="Center" FontSize="14" Margin="5,0,3,0"/>
                <TextBox Name="ExceptionContentFilterTextBox_Tab" Width="150" Height="32" FontSize="14" 
                         ToolTip="Filter EXCEPTION logs by content keywords" 
                         TextChanged="ExceptionContentFilterTextBox_TextChanged" Padding="8,5" VerticalContentAlignment="Center"
                         Margin="0,0,5,0"/>
                <Button Name="ClearExceptionContentFilterButton_Tab" Content="??" Width="28" Height="30" FontSize="13"
                        Click="ClearExceptionContentFilterButton_Click" ToolTip="Clear EXCEPTION Content filter" Margin="3,0"/>
            </StackPanel>
        </GroupBox.Header>
        <ContentControl Name="DataGridHost_EXCEPTION_Tab" />
    </GroupBox>
</TabItem>
```

**REPLACE WITH**:
```xaml
<TabItem Header="EXCEPTION">
    <ContentControl Name="DataGridHost_EXCEPTION_Tab" />
</TabItem>
```

## Step 6: Final Result

After all replacements, your `DataGridTabPanel` section should look like this:

```xaml
<!-- 4개의 탭 DataGrid Area -->
<TabControl Grid.Row="1" Name="DataGridTabPanel" Visibility="Collapsed" Margin="2">
    <TabItem Header="DATA">
        <ContentControl Name="DataGridHost_DATA_Tab" />
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

## Step 7: Test the Changes

After making these changes:

1. **Build the solution** to ensure no errors
2. **Run the application**
3. **Test the toggle button** to switch between 4-panel and tab views
4. **Verify that filters work** in both modes (they should move with the GroupBox)

## What This Achieves

? **Eliminates duplicate controls** - No more `_Tab` suffixed controls  
? **Single source of truth** - Filters in GroupBox headers work in both views  
? **Cleaner XAML** - Much simpler tab structure  
? **Better maintainability** - Changes to filters only need to be made once  
? **Proper encapsulation** - Entire GroupBox (with header and content) moves as a unit

## Controls Removed

The following controls are completely removed from XAML:
- `DataBusinessFilterComboBox_Tab`
- `ClearDataFilterButton_Tab`
- `EventMsgIdFilterComboBox_Tab`
- `ClearEventFilterButton_Tab`
- `DebugContentFilterTextBox_Tab`
- `ClearDebugFilterButton_Tab`
- `ExceptionBusinessFilterComboBox_Tab`
- `ExceptionContentFilterTextBox_Tab`
- `ClearExceptionFilterButton_Tab`
- `ClearExceptionContentFilterButton_Tab`

## C# Code Already Updated

The C# code has been updated in `TryPlaceDataGridIntoActiveHost()` method to:
- Move entire GroupBoxes instead of individual DataGrids
- Properly handle Grid.Column and Grid.Row properties
- Clear hosts when switching modes
- Update visibility appropriately

No additional C# changes are needed!
