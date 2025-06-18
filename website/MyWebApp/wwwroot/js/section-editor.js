(() => {
    const editors = new WeakMap();

    function init(container) {
        if (!container) return;
        const index = container.dataset.index;
        const typeSelect = container.querySelector(`#type-select-${index}`);
        const htmlDiv = container.querySelector(`#html-editor-${index}`);
        const markdownDiv = container.querySelector(`#markdown-editor-${index}`);
        const codeDiv = container.querySelector(`#code-editor-${index}`);
        const fileDiv = container.querySelector(`#file-editor-${index}`);
        const quillInput = container.querySelector(`#Html-${index}`);
        if (!typeSelect || !quillInput) return;
        const quill = new Quill(`#quill-editor-${index}`, { theme: 'snow' });
        quill.root.innerHTML = quillInput.value || '';
        quill.on('text-change', () => {
            quillInput.value = quill.root.innerHTML;
            container.dispatchEvent(new Event('input', { bubbles: true }));
        });
        editors.set(container, quill);
        container.dispatchEvent(new CustomEvent('section-editor:ready', { detail: { quill, index } }));

        function update() {
            const type = typeSelect.value;
            if (htmlDiv) htmlDiv.style.display = type === 'Html' ? 'block' : 'none';
            if (markdownDiv) markdownDiv.style.display = type === 'Markdown' ? 'block' : 'none';
            if (codeDiv) codeDiv.style.display = type === 'Code' ? 'block' : 'none';
            if (fileDiv) fileDiv.style.display = (type === 'Image' || type === 'Video') ? 'block' : 'none';
        }
        typeSelect.addEventListener('change', update);
        update();
    }

    document.addEventListener('DOMContentLoaded', () => {
        document.querySelectorAll('.section-editor').forEach(init);
    });

    document.addEventListener('section-editor:add', e => init(e.detail));
})();
