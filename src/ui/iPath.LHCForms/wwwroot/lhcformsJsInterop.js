// load Questionnaire as form
export function loadForm(questionnaireJson, componentId, options) {
    LForms.Util.addFormToPage(questionnaireJson, componentId, options);
}


// get user entered data as QuestionnaireResponse
export function getData(componentId, options) {
    var response = LForms.Util.getFormFHIRData('QuestionnaireResponse', 'R4', componentId, options);
    return JSON.stringify(response, null, 2);
}

// load form and data
export function loadData(questionnaireJson, responseJson, componentId, options) {
    const qData = JSON.parse(questionnaireJson);
    const qrData = responseJson ? JSON.parse(responseJson) : null;
    LForms.Util.addFormToPage(qData, componentId, { questionnaireResponse: qrData });
}