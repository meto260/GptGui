using System;
using System.Collections.ObjectModel;

namespace GptGui.Models {
    public class ChatSession {
        public string ModelName { get; set; }
        public string SessionName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public ObservableCollection<ChatMessage> Messages { get; set; } = new ObservableCollection<ChatMessage>();
    }
}