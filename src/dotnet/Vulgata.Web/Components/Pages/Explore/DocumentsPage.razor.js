// Read once on entry or explicit reload, never on window resize.
export function measurePageSize(root) {
    const grid = root?.querySelector('.document-list');
    const card = grid?.querySelector('li');
    const controls = root?.querySelector('.browse-actions');
    if (!grid || !card || !controls) return 20;
    const style = getComputedStyle(grid);
    const columns = Math.max(1, style.gridTemplateColumns.split(/\s+/).filter(value => parseFloat(value) > 0).length);
    const gap = parseFloat(style.rowGap) || 0;
    const rowHeight = card.getBoundingClientRect().height;
    if (!rowHeight) return 20;
    const padding = parseFloat(getComputedStyle(root).paddingBottom) || 0;
    const viewport = window.visualViewport?.height ?? window.innerHeight;
    const available = viewport - grid.getBoundingClientRect().top - controls.getBoundingClientRect().height
        - padding - 24;
    const rows = Math.max(1, Math.floor((available + gap) / (rowHeight + gap)));
    return Math.min(200, columns * rows);
}
