window.addEventListener('load', () => {
    const container = document.getElementById('sections-container');
    if (!container) return;
    const templateHtml = document.getElementById('section-template').innerHTML.trim();
    let sectionCount = container.querySelectorAll('.section-editor').length;
    const editors = new Map();
    document.addEventListener('section-editor:ready', e => {
        editors.set(e.detail.index, e.detail.quill);
        e.detail.quill.on('text-change', schedulePreview);
    });
    let activeIndex = null;
    const layoutSelect = document.getElementById('layout-select');
    let currentLayout = layoutSelect ? layoutSelect.value : 'single-column';
    const pageEditor = document.querySelector('.page-editor');
    const previewWrapper = document.getElementById('preview-wrapper');
    const previewFrame = document.getElementById('preview-frame');
    const unsavedIndicator = document.getElementById('unsaved-indicator');
    const modeEdit = document.getElementById('mode-edit');
    const modePreview = document.getElementById('mode-preview');
    const deviceBtns = document.querySelectorAll('.device-btn');
    const previewContainer = document.getElementById('preview-container');
    let dirty = false;
    let previewTimer = null;

    function buildGroups() {
        container.innerHTML = '';
        (layoutZones[currentLayout] || []).forEach(z => {
            const group = document.createElement('div');
            group.className = 'zone-group';
            group.dataset.zone = z;
            const h = document.createElement('h3');
            h.innerHTML = `<span class="zone-name">${z}</span> <span class="zone-count"></span>`;
            const div = document.createElement('div');
            div.className = 'zone-sections';
            group.appendChild(h);
            group.appendChild(div);
            container.appendChild(group);
        });
    }

    function updateZoneCounts() {
        document.querySelectorAll('.zone-group').forEach(g => {
            const count = g.querySelectorAll('.section-editor').length;
            const span = g.querySelector('.zone-count');
            if (span) span.textContent = `(${count})`;
        });
    }

    function collectData() {
        const zones = {};
        document.querySelectorAll('.zone-group').forEach(g => {
            const zone = g.dataset.zone;
            const html = Array.from(g.querySelectorAll('.section-editor')).map(sec => {
                const idx = sec.dataset.index;
                const typeSel = document.getElementById(`type-select-${idx}`);
                if (typeSel && typeSel.value === 'Html') {
                    const q = editors.get(idx);
                    return q ? q.root.innerHTML : '';
                }
                const inp = document.getElementById(`Html-${idx}`);
                return inp ? inp.value : '';
            }).join('\n');
            zones[zone] = html;
        });
        return {
            layout: layoutSelect ? layoutSelect.value : 'single-column',
            title: document.getElementById('title-input')?.value || '',
            zones
        };
    }

    function renderPreview() {
        const data = collectData();
        fetch('/AdminContent/Preview', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
            },
            body: JSON.stringify(data)
        })
            .then(r => r.text())
            .then(html => {
                if (previewFrame) previewFrame.srcdoc = html;
                dirty = false;
                if (unsavedIndicator) unsavedIndicator.style.display = 'none';
            });
    }

    function schedulePreview() {
        dirty = true;
        if (unsavedIndicator) unsavedIndicator.style.display = 'inline';
        clearTimeout(previewTimer);
        previewTimer = setTimeout(renderPreview, 300);
    }

    function populateZones(select) {
        if (!select) return;
        const current = select.dataset.selected || select.value;
        select.innerHTML = (layoutZones[currentLayout] || []).map(z => `<option value="${z}">${z}</option>`).join('');
        if (current) select.value = current;
        select.dataset.selected = '';
    }

    function placeSection(section) {
        const select = section.querySelector('.zone-select');
        const zone = select ? select.value : 'main';
        const group = container.querySelector(`.zone-group[data-zone='${zone}'] .zone-sections`);
        if (group) group.appendChild(section);
    }

    function updatePreview() {
        const preview = document.getElementById('layout-preview');
        if (!preview) return;
        preview.innerHTML = '';
        (layoutZones[currentLayout] || []).forEach(z => {
            const div = document.createElement('div');
            div.className = 'preview-zone';
            div.dataset.zone = z;
            const count = container.querySelectorAll(`.zone-group[data-zone='${z}'] .section-editor`).length;
            div.textContent = `${z} (${count})`;
            preview.appendChild(div);
        });
    }

    document.getElementById('layout-preview')?.addEventListener('click', e => {
        const zone = e.target.closest('.preview-zone');
        if (!zone) return;
        const group = container.querySelector(`.zone-group[data-zone='${zone.dataset.zone}']`);
        if (group) group.scrollIntoView({ behavior: 'smooth' });
        if (activeIndex !== null) {
            const select = document.querySelector(`.zone-select[data-index='${activeIndex}']`);
            if (select) {
                select.value = zone.dataset.zone;
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
            if (activeIndex === null || !editors.has(activeIndex)) {
                alert('Select a section first');
                templateSelect.value = '';
                return;
            }
            fetch(`/AdminBlockTemplate/Html/${id}`)
                .then(r => r.text())
                .then(html => {
                    const quill = editors.get(activeIndex);
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
        populateZones(el.querySelector('.zone-select'));
        placeSection(el);
        document.dispatchEvent(new CustomEvent('section-editor:add', { detail: el }));
    });
    updatePreview();
    updateZoneCounts();

    document.getElementById('add-section').addEventListener('click', () => {
        addSection();
    });

    layoutSelect?.addEventListener('change', () => {
        currentLayout = layoutSelect.value;
        buildGroups();
        document.querySelectorAll('.section-editor').forEach(sec => {
            populateZones(sec.querySelector('.zone-select'));
            placeSection(sec);
        });
        updateIndexes();
        updatePreview();
        updateZoneCounts();
    });

    container.addEventListener('click', e => {
        if (e.target.classList.contains('remove-section')) {
            e.target.closest('.section-editor').remove();
            updateIndexes();
            updateZoneCounts();
        } else if (e.target.classList.contains('duplicate-section')) {
            const original = e.target.closest('.section-editor');
            duplicateSection(original);
        } else if (e.target.classList.contains('add-library')) {
            const section = e.target.closest('.section-editor');
            const idx = section.dataset.index;
            const name = prompt('Block name');
            if (!name) return;
            const q = editors.get(idx);
            const html = q ? q.root.innerHTML : '';
            const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
            fetch('/AdminBlockTemplate/CreateFromSection', {
                method: 'POST',
                headers: { 'RequestVerificationToken': token },
                body: new URLSearchParams({ name, html })
            }).then(() => alert('Saved to library'));
        }
    });

    container.addEventListener('change', e => {
        if (e.target.classList.contains('zone-select')) {
            const section = e.target.closest('.section-editor');
            placeSection(section);
            updateIndexes();
            updateZoneCounts();
        }
    });

    function addSection(htmlContent = '', zone = null) {
        const index = sectionCount++;
        const html = templateHtml.replace(/__index__/g, index);
        const temp = document.createElement('div');
        temp.innerHTML = html;
        const section = temp.firstElementChild;
        section.dataset.index = index;
        populateZones(section.querySelector('.zone-select'));
        placeSection(section);
        document.dispatchEvent(new CustomEvent('section-editor:add', { detail: section }));
        if (htmlContent) {
            const q = editors.get(index);
            if (q) q.root.innerHTML = htmlContent;
            const input = document.getElementById(`Html-${index}`);
            if (input) input.value = htmlContent;
        }
        if (zone) {
            const select = section.querySelector('.zone-select');
            if (select) { select.value = zone; }
            placeSection(section);
        }
        updateIndexes();
        updatePreview();
        updateZoneCounts();
        return index;
    }

    function addSectionFromBlock(id, zone) {
        fetch(`/AdminBlockTemplate/Html/${id}`)
            .then(r => r.text())
            .then(html => {
                addSection(html, zone);
                autoSave();
            });
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
        populateZones(clone.querySelector('.zone-select'));
        placeSection(clone);
        document.dispatchEvent(new CustomEvent('section-editor:add', { detail: clone }));
        const orig = editors.get(original.dataset.index);
        const copy = editors.get(index);
        if (orig && copy) {
            copy.root.innerHTML = orig.root.innerHTML;
            const destInput = clone.querySelector(`#Html-${index}`);
            if (destInput) destInput.value = copy.root.innerHTML;
        }
        updateIndexes();
        updatePreview();
        updateZoneCounts();
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
    let draggedBlockId = null;
    document.addEventListener('blockdragstart', e => {
        draggedBlockId = e.detail;
    });
    const dropIndicator = document.createElement('div');
    dropIndicator.className = 'drop-indicator';

    container.addEventListener('dragstart', e => {
        dragged = e.target.closest('.section-editor');
        draggedBlockId = null;
        if (dragged) {
            dragged.classList.add('dragging');
            document.querySelectorAll('.zone-group').forEach(z => z.classList.add('drag-over'));
        }
        e.dataTransfer.effectAllowed = 'move';
    });

    container.addEventListener('dragover', e => {
        e.preventDefault();
        const zone = e.target.closest('.zone-group');
        const target = e.target.closest('.section-editor');
        if (zone) zone.classList.add('drag-over');
        if (!draggedBlockId && dragged && target && target !== dragged) {
            const rect = target.getBoundingClientRect();
            const next = (e.clientY - rect.top) > (rect.height / 2);
            target.parentNode.insertBefore(dropIndicator, next ? target.nextSibling : target);
        }
    });

    ['dragleave', 'drop'].forEach(evt => {
        container.addEventListener(evt, e => {
            const zone = e.target.closest('.zone-group');
            if (zone) zone.classList.remove('drag-over');
        });
    });

    container.addEventListener('drop', e => {
        e.preventDefault();
        const zone = e.target.closest('.zone-group');
        if (draggedBlockId && zone) {
            addSectionFromBlock(draggedBlockId, zone.dataset.zone);
            draggedBlockId = null;
            document.querySelectorAll('.zone-group.drag-over').forEach(z => z.classList.remove('drag-over'));
            return;
        }
        if (dropIndicator.parentNode) {
            dropIndicator.parentNode.insertBefore(dragged, dropIndicator);
            dropIndicator.remove();
        }
        document.querySelectorAll('.zone-group.drag-over').forEach(z => z.classList.remove('drag-over'));
        if (dragged) dragged.classList.remove('dragging');
        dragged = null;
        updateIndexes();
        updateZoneCounts();
    });

    const form = document.querySelector('form');
    if (form) {
        form.addEventListener('submit', () => {
            editors.forEach((quill, key) => {
                const typeSelect = document.getElementById(`type-select-${key}`);
                if (typeSelect && typeSelect.value === 'Html') {
                    const input = document.getElementById(`Html-${key}`);
                    if (input) input.value = quill.root.innerHTML;
                }
            });
        });
    }

    function autoSave() {
        if (!form) return;
        const fd = new FormData(form);
        fetch(form.action, { method: 'POST', body: fd });
    }


    modePreview?.addEventListener('click', () => {
        pageEditor?.classList.add('preview');
        renderPreview();
    });

    modeEdit?.addEventListener('click', () => {
        pageEditor?.classList.remove('preview');
    });

    deviceBtns.forEach(btn => {
        btn.addEventListener('click', () => {
            deviceBtns.forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            if (previewContainer) previewContainer.style.width = btn.dataset.width;
        });
    });

    document.querySelector('.editor-main')?.addEventListener('input', schedulePreview);
    document.querySelector('.editor-main')?.addEventListener('change', schedulePreview);
    form?.addEventListener('submit', () => {
        dirty = false;
        if (unsavedIndicator) unsavedIndicator.style.display = 'none';
    });
});
