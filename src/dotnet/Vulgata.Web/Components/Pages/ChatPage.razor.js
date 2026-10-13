export function prefersReducedMotion() {
    return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

export function scrollConversationToEnd(conversationElement) {
    if (!conversationElement) {
        return;
    }

    conversationElement.scrollTop = conversationElement.scrollHeight;
}


const composers = new WeakMap();

export function initializeOctoberComposer(root) {
    if (!root || composers.has(root)) return;
    const input = root.querySelector('#october-chat-question');
    const conversation = root.querySelector('.chat-surface');
    if (!input || !conversation) return;
    const lifetime = new AbortController();
    const options = { signal: lifetime.signal };
    let following = false;
    let composing = false;
    let frame = 0;
    let expectedTop = conversation.scrollTop;
    const stopFollowing = () => { following = false; };
    const schedule = () => {
        if (!following || frame) return;
        frame = requestAnimationFrame(() => {
            frame = 0;
            if (!following) return;
            conversation.scrollTop = conversation.scrollHeight;
            expectedTop = conversation.scrollTop;
        });
    };
    root.addEventListener('click', event => {
        const send = event.target.closest('[data-chat-send]');
        if (!send || send.hasAttribute('disabled') || input.disabled || !input.value.trim()) return;
        following = true;
        schedule();
    }, { ...options, capture: true });
    input.addEventListener('compositionstart', () => { composing = true; }, options);
    input.addEventListener('compositionend', () => { composing = false; }, options);
    input.addEventListener('keydown', event => {
        if (event.key !== 'Enter' || event.isComposing || composing || event.keyCode === 229 || input.disabled) return;
        event.preventDefault();
        if (event.repeat) return;
        if (event.ctrlKey) {
            input.setRangeText('\n', input.selectionStart, input.selectionEnd, 'end');
            input.dispatchEvent(new Event('input', { bubbles: true }));
            return;
        }
        if (event.altKey || event.metaKey) return;
        const send = root.querySelector('[data-chat-send]');
        if (input.value.trim() && send && !send.hasAttribute('disabled')) send.click();
    }, options);
    conversation.addEventListener('wheel', stopFollowing, { ...options, passive: true });
    conversation.addEventListener('touchmove', stopFollowing, { ...options, passive: true });
    conversation.addEventListener('keydown', event => {
        if (['ArrowUp', 'ArrowDown', 'PageUp', 'PageDown', 'Home', 'End', ' '].includes(event.key)) stopFollowing();
    }, options);
    conversation.addEventListener('scroll', () => {
        // Programmatic following must not count as the user's first scroll.
        if (Math.abs(conversation.scrollTop - expectedTop) > 1) stopFollowing();
    }, { ...options, passive: true });
    const observer = new MutationObserver(schedule);
    observer.observe(conversation, { childList: true, subtree: true, characterData: true });
    const resize = new ResizeObserver(schedule);
    resize.observe(conversation);
    composers.set(root, () => {
        lifetime.abort();
        observer.disconnect();
        resize.disconnect();
        cancelAnimationFrame(frame);
    });
}

export function disposeOctoberComposer(root) {
    composers.get(root)?.();
    composers.delete(root);
}
