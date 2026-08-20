using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

namespace TorProduction.AddressablesToolpack {
    [Serializable]
    public class SerializableDictionary<TKey, TValue> : ISerializationCallbackReceiver {
        [SerializeField] private List<KeyValueEntry> m_entries = new List<KeyValueEntry>();
        private Dictionary<TKey, TValue> m_dictionary = new Dictionary<TKey, TValue>();
        public Dictionary<TKey, TValue> Dictionary => m_dictionary;

        [Serializable]
        class KeyValueEntry {
            public TKey key;
            public TValue value;
        }

        public void OnAfterDeserialize() {
            m_dictionary.Clear();
            foreach (var entry in m_entries) {
                if (entry.key == null) {
                    continue;
                }

                if (!m_dictionary.ContainsKey(entry.key)) {
                    m_dictionary.Add(entry.key, entry.value);
                }
            }
        }

        public void OnBeforeSerialize() {
            UpdateEntriesFromDictionary();
            DetectAndWarnAboutDuplicateKeys();
        }

        private void UpdateEntriesFromDictionary() {
            // Update or add new entries to match the current state of the dictionary
            foreach (var pair in m_dictionary) {
                var entry = m_entries.FirstOrDefault(e => EqualityComparer<TKey>.Default.Equals(e.key, pair.Key));
                if (entry != null) {
                    entry.value = pair.Value;
                } else {
                    m_entries.Add(new KeyValueEntry { key = pair.Key, value = pair.Value });
                }
            }

            // Remove any entries that no longer exist in the dictionary
            m_entries.RemoveAll(entry => !m_dictionary.ContainsKey(entry.key));
        }

        private void DetectAndWarnAboutDuplicateKeys() {
            var duplicateKeys = m_entries.Select(e => e.key)
                .GroupBy(x => x)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToArray();
            if (duplicateKeys.Any()) {
                Debug.LogWarning(
                    $"Warning: Found duplicate keys in SerializableDictionary: {string.Join(", ", duplicateKeys)}");
            }
        }
    }
}
