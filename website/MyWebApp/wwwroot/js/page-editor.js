window.addEventListener('load', () => {
    const container = document.getElementById('sections-container');
    if (!container) return;
    const templateHtml = document.getElementById('section-template').innerHTML.trim();

    document.getElementById('add-section').addEventListener('click', () => {
        addSection();
    });

    container.addEventListener('click', (e) => {
        if (e.target.classList.contains('remove-section')) {
            e.target.closest('.section-editor').remove();
            updateIndexes();
        } else if (e.target.classList.contains('duplicate-section')) {
            const original = e.target.closest('.section-editor');
            const clone = original.cloneNode(true);
            container.insertBefore(clone, original.nextSibling);
            updateIndexes();
        }
    });

    function addSection() {
        const index = container.querySelectorAll('.section-editor').length;
        const html = templateHtml.replace(/__index__/g, index);
        const temp = document.createElement('div');
        temp.innerHTML = html;
        container.appendChild(temp.firstElementChild);
        updateIndexes();
    }

    function updateIndexes() {
        container.querySelectorAll('.section-editor').forEach((el, idx) => {
            el.querySelectorAll('input, textarea').forEach(input => {
                input.name = input.name.replace(/Sections\[.*?\]/, `Sections[${idx}]`);
            });
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
});
