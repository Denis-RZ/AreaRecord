window.addEventListener('load', () => {
    const panel = document.getElementById('block-library');
    if (!panel) return;
    const search = document.getElementById('block-search');
    const list = panel.querySelector('.block-list');
    let blocks = [];
    fetch('/AdminBlockTemplate/GetBlocks')
        .then(r => r.json())
        .then(data => { blocks = data; render(blocks); });

    function render(items) {
        list.innerHTML = items.map(b => {
            const encoded = b.preview.replace(/</g, '&lt;').replace(/>/g, '&gt;');
            return `<div class="block-card" draggable="true" data-id="${b.id}">
                <div class="block-name">${b.name}</div>
                <div class="block-preview">${encoded}</div>
                <button type="button" class="quick-edit" data-id="${b.id}">Edit</button>
            </div>`;
        }).join('');
    }

    search?.addEventListener('input', () => {
        const q = search.value.toLowerCase();
        const filtered = blocks.filter(b => b.name.toLowerCase().includes(q));
        render(filtered);
    });

    list.addEventListener('click', e => {
        const btn = e.target.closest('.quick-edit');
        if (btn) {
            const id = btn.dataset.id;
            window.location = `/AdminBlockTemplate/Edit/${id}`;
        }
    });

    list.addEventListener('dragstart', e => {
        const card = e.target.closest('.block-card');
        if (!card) return;
        const ev = new CustomEvent('blockdragstart', { detail: card.dataset.id });
        document.dispatchEvent(ev);
        e.dataTransfer.effectAllowed = 'copy';
    });
});
