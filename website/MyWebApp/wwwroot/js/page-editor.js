window.addEventListener('load', () => {
    const container = document.getElementById('sections-container');
    if (!container) return;
    const templateHtml = document.getElementById('section-template').innerHTML.trim();
    let sectionCount = container.querySelectorAll('.section-editor').length;
    const editors = {};
    let activeIndex = null;
    const layoutSelect = document.getElementById('layout-select');
    let currentLayout = layoutSelect ? layoutSelect.value : 'single-column';

    function buildGroups() {
        container.innerHTML = '';
        (layoutZones[currentLayout] || []).forEach(a => {
            const group = document.createElement('div');
            group.className = 'area-group';
            group.dataset.area = a;
            const h = document.createElement('h3');
            h.textContent = a;
            const div = document.createElement('div');
            div.className = 'area-sections';
            group.appendChild(h);
            group.appendChild(div);
            container.appendChild(group);
        });
    }

    function populateAreas(select) {
        if (!select) return;
        const current = select.dataset.selected || select.value;
        select.innerHTML = (layoutZones[currentLayout] || []).map(a => `<option value="${a}">${a}</option>`).join('');
        if (current) select.value = current;
        select.dataset.selected = '';
    }

    function placeSection(section) {
        const select = section.querySelector('.area-select');
        const area = select ? select.value : 'main';
        const group = container.querySelector(`.area-group[data-area='${area}'] .area-sections`);
        if (group) group.appendChild(section);
    }

    function updatePreview() {
        const preview = document.getElementById('layout-preview');
        if (!preview) return;
        preview.innerHTML = '';
        (layoutZones[currentLayout] || []).forEach(a => {
            const div = document.createElement('div');
            div.className = 'preview-zone';
            div.dataset.area = a;
            div.textContent = a;
            preview.appendChild(div);
        });
    }

    document.getElementById('layout-preview')?.addEventListener('click', e => {
        const zone = e.target.closest('.preview-zone');
        if (!zone) return;
        if (activeIndex !== null) {
            const select = document.querySelector(`.area-select[data-index='${activeIndex}']`);
            if (select) {
                select.value = zone.dataset.area;
                placeSection(select.closest('.section-editor'));
                updateIndexes();
            }
        }
    });

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

    const existing = Array.from(container.querySelectorAll('.section-editor'));
    buildGroups();
    existing.forEach(el => {
        const idx = el.dataset.index;
        populateAreas(el.querySelector('.area-select'));
        placeSection(el);
        initSectionEditor(idx);
    });
    updatePreview();

    document.getElementById('add-section').addEventListener('click', () => {
        addSection();
    });

    layoutSelect?.addEventListener('change', () => {
        currentLayout = layoutSelect.value;
        buildGroups();
        document.querySelectorAll('.section-editor').forEach(sec => {
            populateAreas(sec.querySelector('.area-select'));
            placeSection(sec);
        });
        updateIndexes();
        updatePreview();
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

    container.addEventListener('change', e => {
        if (e.target.classList.contains('area-select')) {
            const section = e.target.closest('.section-editor');
            placeSection(section);
            updateIndexes();
        }
    });

    function addSection() {
        const index = sectionCount++;
        const html = templateHtml.replace(/__index__/g, index);
        const temp = document.createElement('div');
        temp.innerHTML = html;
        const section = temp.firstElementChild;
        section.dataset.index = index;
        populateAreas(section.querySelector('.area-select'));
        placeSection(section);
        initSectionEditor(index);
        updateIndexes();
        updatePreview();
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
        populateAreas(clone.querySelector('.area-select'));
        placeSection(clone);
        initSectionEditor(index);
        if (editors[original.dataset.index]) {
            editors[index].root.innerHTML = editors[original.dataset.index].root.innerHTML;
            const destInput = clone.querySelector(`#Html-${index}`);
            if (destInput) destInput.value = editors[index].root.innerHTML;
        }
        updateIndexes();
        updatePreview();
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
            target.parentNode.insertBefore(dragged, next ? target.nextSibling : target);
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
