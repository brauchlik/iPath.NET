window.focusElement = (elementId) => {
    var el = document.getElementById(elementId);
    el.focus();
};

window.getElementDimensions = (element) => {
    return {
        width: element.offsetWidth,
        height: element.offsetHeight
    };
};
