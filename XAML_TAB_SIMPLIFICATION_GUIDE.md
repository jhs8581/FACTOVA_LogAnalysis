# XAML Tab Simplification Guide

## Problem
Current XAML has redundant filter controls in both:
1. The 4-panel GroupBox headers (e.g., `DataBusinessFilterComboBox`)
2. The tab view GroupBox headers (e.g., `DataBusinessFilterComboBox_Tab`)

## Solution
Remove ALL `_Tab` filter controls from the tab structure. The entire GroupBox (including its header with filters) will now move between 4-panel and tab views.

## New Tab Structure (lines ~730-780 in MainWindow.xaml)

Replace the existing `DataGridTabPanel` TabControl content with this simplified structure:

```xaml
<!-- 4°³ÀÇ ÅÇ DataGrid Area -->
<TabControl Grid.Row="1" Name="DataGridTabPanel" Visibility="Collapsed" Margin="2">
    <TabItem Header="DATA">
        <!-- Host for entire DATA GroupBox (no filters needed here) -->
        <ContentControl Name="DataGridHost_DATA_Tab" />
    </TabItem>
    <TabItem Header="EVENT">
        <!-- Host for entire EVENT GroupBox (no filters needed here) -->
        <ContentControl Name="DataGridHost_EVENT_Tab" />
    </TabItem>
    <TabItem Header="DEBUG">
        <!-- Host for entire DEBUG GroupBox (no filters needed here) -->
        <ContentControl Name="DataGridHost_DEBUG_Tab" />
    </TabItem>
    <TabItem Header="EXCEPTION">
        <!-- Host for entire EXCEPTION GroupBox (no filters needed here) -->
        <ContentControl Name="DataGridHost_EXCEPTION_Tab" />
    </TabItem>
</TabControl>
```

## Controls to Remove from XAML

### From DATA Tab (lines ~735-750):
- Remove: `DataBusinessFilterComboBox_Tab`
- Remove: `ClearDataFilterButton_Tab`
- Remove: All associated TextBlocks and layout controls

### From EVENT Tab (lines ~758-773):
- Remove: `EventMsgIdFilterComboBox_Tab`
- Remove: `ClearEventFilterButton_Tab`
- Remove: All associated TextBlocks and layout controls

### From DEBUG Tab (lines ~781-796):
- Remove: `DebugContentFilterTextBox_Tab`
- Remove: `ClearDebugFilterButton_Tab`
- Remove: All associated TextBlocks and layout controls

### From EXCEPTION Tab (lines ~804-825):
- Remove: `ExceptionBusinessFilterComboBox_Tab`
- Remove: `ExceptionContentFilterTextBox_Tab`
- Remove: `ClearExceptionFilterButton_Tab` and `ClearExceptionContentFilterButton_Tab`
- Remove: All associated TextBlocks and layout controls

## Result
- The filter controls in the 4-panel GroupBox headers will automatically be available in tab view
- No duplicate controls needed
- Simpler XAML structure
- Single source of truth for filters

## C# Code Already Updated
The `TryPlaceDataGridIntoActiveHost()` method now moves entire GroupBoxes (with their headers and filters) between views.
