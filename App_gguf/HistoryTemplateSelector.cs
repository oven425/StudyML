using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace App_gguf
{
    /// <summary>
    /// Picks the chat bubble template (user vs. AI) for each <see cref="History"/> item
    /// so the conversation renders like a GitHub Copilot style chat sequence.
    /// </summary>
    public sealed class HistoryTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? UserTemplate { get; set; }
        public DataTemplate? AssistantTemplate { get; set; }
        public DataTemplate? ToolCallTemplate { get; set; }
        public DataTemplate? ToolResultTemplate { get; set; }

        public DataTemplate? ToolstTemplate { get; set; }

        protected override DataTemplate? SelectTemplateCore(object item)
            => item is not History history
                ? AssistantTemplate
                : history.Role switch
                {
                    History.RoleKind.User => UserTemplate,
                    History.RoleKind.ToolCall => ToolCallTemplate,
                    History.RoleKind.ToolResult => ToolResultTemplate,
                    History.RoleKind.Tools=>ToolstTemplate,
                    _ => AssistantTemplate
                };

        protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
            => SelectTemplateCore(item);
    }
}
