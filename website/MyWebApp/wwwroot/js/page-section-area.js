window.addEventListener('load', () => {
    const pageSelect = document.querySelector('select[name="PageId"]');
    const areaSelect = document.getElementById('area-select');
    if (!pageSelect || !areaSelect) return;

    function loadAreas() {
        const id = pageSelect.value;
        if (!id) { areaSelect.innerHTML = ''; return; }
        fetch(`/AdminPageSection/GetAreasForPage/${id}`)
            .then(r => r.json())
            .then(list => {
                areaSelect.innerHTML = list.map(a => `<option value="${a}">${a}</option>`).join('');
                if (areaSelect.dataset.selected)
                    areaSelect.value = areaSelect.dataset.selected;
            });
    }
    loadAreas();
    pageSelect.addEventListener('change', loadAreas);
});
