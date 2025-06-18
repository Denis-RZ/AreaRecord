window.addEventListener('load', () => {
    const html = document.getElementById('html-area');
    if (!html) return;
    const blockBtn = document.getElementById('insert-block-btn');
    const blockSelect = document.getElementById('block-select');
    const sectionBtn = document.getElementById('insert-section-btn');
    const pageSelect = document.getElementById('page-select');
    const sectionSelect = document.getElementById('section-select');

    blockBtn?.addEventListener('click', () => {
        fetch('/Api/GetBlocks')
            .then(r => r.json())
            .then(list => {
                blockSelect.innerHTML = '<option value="">select</option>' +
                    list.map(b => `<option value="${b.id}">${b.name}</option>`).join('');
                blockSelect.style.display = 'inline';
                blockSelect.focus();
            });
    });

    blockSelect?.addEventListener('change', () => {
        const id = blockSelect.value;
        if (!id) return;
        insertToken(`{{block:${id}}}`);
        blockSelect.style.display = 'none';
    });

    sectionBtn?.addEventListener('click', () => {
        fetch('/Api/GetPages')
            .then(r => r.json())
            .then(list => {
                pageSelect.innerHTML = '<option value="">page</option>' +
                    list.map(p => `<option value="${p.id}">${p.slug}</option>`).join('');
                pageSelect.style.display = 'inline';
                sectionSelect.style.display = 'none';
                pageSelect.focus();
            });
    });

    pageSelect?.addEventListener('change', () => {
        const id = pageSelect.value;
        if (!id) return;
        fetch(`/Api/GetSections/${id}`)
            .then(r => r.json())
            .then(list => {
                sectionSelect.innerHTML = '<option value="">area</option>' +
                    list.map(a => `<option value="${a}">${a}</option>`).join('');
                sectionSelect.style.display = 'inline';
                sectionSelect.focus();
            });
    });

    sectionSelect?.addEventListener('change', () => {
        const pageId = pageSelect.value;
        const area = sectionSelect.value;
        if (!pageId || !area) return;
        insertToken(`{{section:${pageId}:${area}}}`);
        pageSelect.style.display = 'none';
        sectionSelect.style.display = 'none';
    });

    function insertToken(text) {
        if (html.selectionStart !== undefined) {
            const start = html.selectionStart;
            const end = html.selectionEnd;
            html.value = html.value.substring(0, start) + text + html.value.substring(end);
            html.selectionStart = html.selectionEnd = start + text.length;
        } else {
            html.value += text;
        }
    }
});
