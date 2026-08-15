export function prefersReducedMotion() {
    return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

export function scrollConversationToEnd(conversationElement) {
    if (!conversationElement) {
        return;
    }

    conversationElement.scrollTop = conversationElement.scrollHeight;
}
