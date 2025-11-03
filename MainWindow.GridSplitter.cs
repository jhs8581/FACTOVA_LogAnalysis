using System;
using System.Windows;
using System.Windows.Controls;
using FACTOVA_LogAnalysis.Services;

namespace FACTOVA_LogAnalysis
{
    public partial class MainWindow
    {
        #region GridSplitter Position Management (moved)

        private void RestoreGridSplitterPositions()
        {
            try
            {
                // Text Tab GridSplitter 위치 복원
                if (_appSettings.LeftColumnWidth > 0 && _appSettings.RightColumnWidth > 0)
                {
                    var leftColumnDef = FindName("LeftColumnDefinition") as ColumnDefinition;
                    var rightColumnDef = FindName("RightColumnDefinition") as ColumnDefinition;

                    if (leftColumnDef != null && rightColumnDef != null)
                    {
                        leftColumnDef.Width = new GridLength(_appSettings.LeftColumnWidth, GridUnitType.Star);
                        rightColumnDef.Width = new GridLength(_appSettings.RightColumnWidth, GridUnitType.Star);
                    }
                }

                if (_appSettings.TopRowHeight > 0 && _appSettings.BottomRowHeight > 0)
                {
                    var topRowDef = FindName("TopRowDefinition") as RowDefinition;
                    var bottomRowDef = FindName("BottomRowDefinition") as RowDefinition;

                    if (topRowDef != null && bottomRowDef != null)
                    {
                        topRowDef.Height = new GridLength(_appSettings.TopRowHeight, GridUnitType.Star);
                        bottomRowDef.Height = new GridLength(_appSettings.BottomRowHeight, GridUnitType.Star);
                    }
                }

                // DataGrid Tab GridSplitter 위치 복원
                if (_appSettings.DataLeftColumnWidth > 0 && _appSettings.DataRightColumnWidth > 0)
                {
                    var dataLeftColumnDef = FindName("DataLeftColumnDefinition") as ColumnDefinition;
                    var dataRightColumnDef = FindName("DataRightColumnDefinition") as ColumnDefinition;

                    if (dataLeftColumnDef != null && dataRightColumnDef != null)
                    {
                        dataLeftColumnDef.Width = new GridLength(_appSettings.DataLeftColumnWidth, GridUnitType.Star);
                        dataRightColumnDef.Width = new GridLength(_appSettings.DataRightColumnWidth, GridUnitType.Star);
                    }
                }

                if (_appSettings.DataTopRowHeight > 0 && _appSettings.DataBottomRowHeight > 0)
                {
                    var dataTopRowDef = FindName("DataTopRowDefinition") as RowDefinition;
                    var dataBottomRowDef = FindName("DataBottomRowDefinition") as RowDefinition;

                    if (dataTopRowDef != null && dataBottomRowDef != null)
                    {
                        dataTopRowDef.Height = new GridLength(_appSettings.DataTopRowHeight, GridUnitType.Star);
                        dataBottomRowDef.Height = new GridLength(_appSettings.DataBottomRowHeight, GridUnitType.Star);
                    }
                }

                _workLogService.AddLog("? GridSplitter 위치 복원 완료", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? GridSplitter 위치 복원 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void SetupGridSplitterEvents()
        {
            try
            {
                // SizeChanged 이벤트는 Window의 SizeChanged에서 처리
                this.SizeChanged += MainWindow_SizeChanged;

                _workLogService.AddLog("? GridSplitter 이벤트 설정 완료", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? GridSplitter 이벤트 설정 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 초기화 중이거나 최소화 상태에서는 저장하지 않음
            if (_isInitializing || this.WindowState == WindowState.Minimized)
                return;

            try
            {
                SaveGridSplitterPositions();
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? GridSplitter 위치 저장 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void SaveGridSplitterPositions()
        {
            try
            {
                // Text Tab GridSplitter 위치 저장
                var leftColumnDef = FindName("LeftColumnDefinition") as ColumnDefinition;
                var rightColumnDef = FindName("RightColumnDefinition") as ColumnDefinition;
                var topRowDef = FindName("TopRowDefinition") as RowDefinition;
                var bottomRowDef = FindName("BottomRowDefinition") as RowDefinition;

                if (leftColumnDef != null && rightColumnDef != null)
                {
                    _appSettings.LeftColumnWidth = leftColumnDef.Width.Value;
                    _appSettings.RightColumnWidth = rightColumnDef.Width.Value;
                }

                if (topRowDef != null && bottomRowDef != null)
                {
                    _appSettings.TopRowHeight = topRowDef.Height.Value;
                    _appSettings.BottomRowHeight = bottomRowDef.Height.Value;
                }

                // DataGrid Tab GridSplitter 위치 저장
                var dataLeftColumnDef = FindName("DataLeftColumnDefinition") as ColumnDefinition;
                var dataRightColumnDef = FindName("DataRightColumnDefinition") as ColumnDefinition;
                var dataTopRowDef = FindName("DataTopRowDefinition") as RowDefinition;
                var dataBottomRowDef = FindName("DataBottomRowDefinition") as RowDefinition;

                if (dataLeftColumnDef != null && dataRightColumnDef != null)
                {
                    _appSettings.DataLeftColumnWidth = dataLeftColumnDef.Width.Value;
                    _appSettings.DataRightColumnWidth = dataRightColumnDef.Width.Value;
                }

                if (dataTopRowDef != null && dataBottomRowDef != null)
                {
                    _appSettings.DataTopRowHeight = dataTopRowDef.Height.Value;
                    _appSettings.DataBottomRowHeight = dataBottomRowDef.Height.Value;
                }

                _appSettings.Save();

                System.Diagnostics.Debug.WriteLine("GridSplitter 위치 저장됨");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GridSplitter 위치 저장 오류: {ex.Message}");
            }
        }

        #endregion
    }
}
