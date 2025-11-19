window.initializeSortable = (elementId, dotNetHelper) => {
    var el = document.getElementById(elementId);
    new Sortable(el, {
        animation: 150,
        onEnd: function (evt) {
            var order = [];
            el.querySelectorAll('.gallery-item').forEach((item, index) => {
                order.push(item.getAttribute('data-id'));
            });
            dotNetHelper.invokeMethodAsync('UpdateSortOrder', order);
        }
    });
};
