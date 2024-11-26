using System;
using System.Collections.Generic;

namespace GptGui.Models {
    public class ChatMessage {
        public string Role { get; set; }
        public string Content { get; set; }
        public bool IsVisible { get; set; } = true;
        public List<string> CodeBlocks { get; set; } = new List<string>();
        public byte[] FileContent { get; set; }
        public string FileName { get; set; }
    }
}