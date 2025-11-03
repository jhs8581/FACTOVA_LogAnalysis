using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using FACTOVA_LogAnalysis.Models;

namespace FACTOVA_LogAnalysis.Services
{
    public class RedBusinessListManager
    {
        private readonly ObservableCollection<RedBusinessItem> _redBusinessItems;
        private const string RED_BUSINESS_LIST_FILE = "RedBusinessList.json";

        public ObservableCollection<RedBusinessItem> Items => _redBusinessItems;

        public RedBusinessListManager()
        {
            _redBusinessItems = new ObservableCollection<RedBusinessItem>();
        }

        public void LoadSampleItems()
        {
            _redBusinessItems.Clear();
            _redBusinessItems.Add(new RedBusinessItem { Index = 1, BusinessName = "BR_SFC_RegisterStartEndJobBuffer", Description = "Job Buffer 등록", IsEnabled = true, Color = "Red" });
            _redBusinessItems.Add(new RedBusinessItem { Index = 2, BusinessName = "BR_SFC_CheckStartLotUI", Description = "Lot UI 체크", IsEnabled = true, Color = "Blue" });
        }

        public void LoadFromFile()
        {
            try
            {
                string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FACTOVA_LogAnalysis");
                string filePath = Path.Combine(appDataPath, RED_BUSINESS_LIST_FILE);

                if (File.Exists(filePath))
                {
                    string jsonString = File.ReadAllText(filePath, Encoding.UTF8);
                    var items = JsonSerializer.Deserialize<List<RedBusinessItem>>(jsonString);
                    
                    _redBusinessItems.Clear();
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            _redBusinessItems.Add(item);
                        }
                    }
                }
                else
                {
                    // 파일이 없으면 샘플 데이터 추가
                    LoadSampleItems();
                }
            }
            catch (Exception ex)
            {
                // 로드 실패시 샘플 데이터 로드
                LoadSampleItems();
                System.Diagnostics.Debug.WriteLine($"빨간색 비즈니스 리스트 로드 실패: {ex.Message}");
                throw new Exception($"빨간색 비즈니스 리스트 로드 실패: {ex.Message}");
            }
        }

        public void SaveToFile()
        {
            try
            {
                string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FACTOVA_LogAnalysis");
                if (!Directory.Exists(appDataPath))
                {
                    Directory.CreateDirectory(appDataPath);
                }

                string filePath = Path.Combine(appDataPath, RED_BUSINESS_LIST_FILE);
                
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                
                string jsonString = JsonSerializer.Serialize(_redBusinessItems.ToList(), jsonOptions);
                File.WriteAllText(filePath, jsonString, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                throw new Exception($"빨간색 비즈니스 리스트 저장 실패: {ex.Message}");
            }
        }

        public void AddItem()
        {
            int newIndex = _redBusinessItems.Count > 0 ? _redBusinessItems.Max(x => x.Index) + 1 : 1;
            
            var newItem = new RedBusinessItem
            {
                Index = newIndex,
                BusinessName = "",
                Description = "",
                IsEnabled = true
            };
            
            _redBusinessItems.Add(newItem);
        }

        public bool RemoveItem(RedBusinessItem item)
        {
            if (_redBusinessItems.Contains(item))
            {
                _redBusinessItems.Remove(item);
                ReorganizeIndices();
                return true;
            }
            return false;
        }

        public void ReorganizeIndices()
        {
            for (int i = 0; i < _redBusinessItems.Count; i++)
            {
                _redBusinessItems[i].Index = i + 1;
            }
        }

        public List<string> GetEnabledBusinessNames()
        {
            return _redBusinessItems
                .Where(item => item.IsEnabled && !string.IsNullOrWhiteSpace(item.BusinessName))
                .Select(item => item.BusinessName.Trim())
                .ToList();
        }

        public List<string> GetEnabledBusinessNamesColor()
        {
            return _redBusinessItems
                .Where(item => item.IsEnabled && !string.IsNullOrWhiteSpace(item.Color))
                .Select(item => item.Color.Trim())
                .ToList();
        }
    }
}