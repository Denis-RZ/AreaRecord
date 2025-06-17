window.addEventListener('load', () => {
    const container = document.getElementById('sections-container');
    if (!container) return;
    const templateHtml = document.getElementById('section-template').innerHTML.trim();
    let sectionCount = container.querySelectorAll('.section-editor').length;
    const editors = {};
    let activeIndex = null;

    const templateSelect = document.getElementById('template-selector');
    if (templateSelect) {
        templateSelect.addEventListener('change', () => {
            const id = templateSelect.value;
            if (!id) return;
            if (activeIndex === null || !editors[activeIndex]) {
                alert('Select a section first');
                templateSelect.value = '';
                return;
            }
            fetch(`/AdminBlockTemplate/Html/${id}`)
                .then(r => r.text())
                .then(html => {
                    const quill = editors[activeIndex];
                    quill.root.innerHTML = html;
                    const input = document.getElementById(`Html-${activeIndex}`);
                    if (input) input.value = html;
                    templateSelect.value = '';
                });
        });
    }

    container.querySelectorAll('.section-editor').forEach(el => {
        const idx = el.dataset.index;
        initSectionEditor(idx);
    });

    document.getElementById('add-section').addEventListener('click', () => {
        addSection();
    });

    container.addEventListener('click', e => {
        if (e.target.classList.contains('remove-section')) {
            e.target.closest('.section-editor').remove();
            updateIndexes();
        } else if (e.target.classList.contains('duplicate-section')) {
            const original = e.target.closest('.section-editor');
            duplicateSection(original);
        }
    });

    function addSection() {
        const index = sectionCount++;
        const html = templateHtml.replace(/__index__/g, index);
        const temp = document.createElement('div');
        temp.innerHTML = html;
        const section = temp.firstElementChild;
        section.dataset.index = index;
        container.appendChild(section);
        initSectionEditor(index);
        updateIndexes();
    }

    function duplicateSection(original) {
        const index = sectionCount++;
        const html = templateHtml.replace(/__index__/g, index);
        const temp = document.createElement('div');
        temp.innerHTML = html;
        const clone = temp.firstElementChild;
        clone.dataset.index = index;
        // copy values
        original.querySelectorAll('input, textarea, select').forEach(src => {
            if (!src.name) return;
            const suffix = src.name.substring(src.name.indexOf('].') + 2);
            const dest = clone.querySelector(`[name$='.${suffix}']`);
            if (!dest || src.type === 'file') return;
            if (src.type === 'checkbox' || src.type === 'radio') {
                dest.checked = src.checked;
            } else {
                dest.value = src.value;
            }
        });
        container.insertBefore(clone, original.nextSibling);
        initSectionEditor(index);
        if (editors[original.dataset.index]) {
            editors[index].root.innerHTML = editors[original.dataset.index].root.innerHTML;
            const destInput = clone.querySelector(`#Html-${index}`);
            if (destInput) destInput.value = editors[index].root.innerHTML;
        }
        updateIndexes();
    }

    function updateIndexes() {
        container.querySelectorAll('.section-editor').forEach((el, idx) => {
            el.querySelectorAll('input, textarea, select').forEach(input => {
                if (input.name)
                    input.name = input.name.replace(/Sections\[\d+\]/, `Sections[${idx}]`);
            });
            const sortInput = el.querySelector('.sort-order');
            if (sortInput) sortInput.value = idx;
        });
    }

    let dragged = null;
    container.addEventListener('dragstart', e => {
        dragged = e.target.closest('.section-editor');
        e.dataTransfer.effectAllowed = 'move';
    });
    container.addEventListener('dragover', e => {
        e.preventDefault();
        const target = e.target.closest('.section-editor');
        if (dragged && target && target !== dragged) {
            const rect = target.getBoundingClientRect();
            const next = (e.clientY - rect.top) > (rect.height / 2);
            container.insertBefore(dragged, next ? target.nextSibling : target);
        }
    });
    container.addEventListener('drop', e => {
        e.preventDefault();
        updateIndexes();
    });

    const form = document.querySelector('form');
    if (form) {
        form.addEventListener('submit', () => {
            Object.entries(editors).forEach(([key, quill]) => {
                const typeSelect = document.getElementById(`type-select-${key}`);
                if (typeSelect && typeSelect.value === 'Html') {
                    const input = document.getElementById(`Html-${key}`);
                    if (input) input.value = quill.root.innerHTML;
                }
            });
        });
    }

    function initSectionEditor(index) {
        const typeSelect = document.getElementById(`type-select-${index}`);
        const htmlDiv = document.getElementById(`html-editor-${index}`);
        const markdownDiv = document.getElementById(`markdown-editor-${index}`);
        const codeDiv = document.getElementById(`code-editor-${index}`);
        const fileDiv = document.getElementById(`file-editor-${index}`);
        const quillInput = document.getElementById(`Html-${index}`);
        const markdown = markdownDiv ? markdownDiv.querySelector('textarea') : null;
        const code = codeDiv ? codeDiv.querySelector('textarea') : null;
        const quill = new Quill(`#quill-editor-${index}`, { theme: 'snow' });
        quill.root.innerHTML = quillInput.value || '';
        quill.root.addEventListener('click', () => { activeIndex = index; });
        quill.root.addEventListener('focus', () => { activeIndex = index; });
        editors[index] = quill;

        function update() {
            const type = typeSelect.value;
            htmlDiv.style.display = type === 'Html' ? 'block' : 'none';
            markdownDiv.style.display = type === 'Markdown' ? 'block' : 'none';
            codeDiv.style.display = type === 'Code' ? 'block' : 'none';
            fileDiv.style.display = (type === 'Image' || type === 'Video') ? 'block' : 'none';
            if (markdown) markdown.disabled = type !== 'Markdown';
            if (code) code.disabled = type !== 'Code';
            quillInput.disabled = type !== 'Html';
            const fileInput = fileDiv ? fileDiv.querySelector('input[type="file"]') : null;
            if (fileInput) fileInput.disabled = !(type === 'Image' || type === 'Video');
        }
        update();
        typeSelect.addEventListener('change', update);
    }
});
