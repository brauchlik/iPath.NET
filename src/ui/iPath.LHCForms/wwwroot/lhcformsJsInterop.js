// LForms is loaded as plain <script> tags in App.razor, so it may not be ready when
// Blazor renders - especially under WebAssembly over a slow link, where OnAfterRender
// can beat the CDN. Calling addFormToPage too early throws, and the form silently
// stays blank or loads without FHIR support (skip logic then never fires).
//
// The FHIR check matters as much as the Util check: lhc-forms.js defines LForms.Util,
// but it is lformsFHIR.min.js that adds LForms.FHIR.R4, and addFormToPage needs both.

const READY_TIMEOUT_MS = 10000;
const POLL_INTERVAL_MS = 100;

function isReady() {
    return typeof LForms !== 'undefined'
        && LForms.Util
        && typeof LForms.Util.addFormToPage === 'function'
        && LForms.FHIR
        && LForms.FHIR.R4;
}

function whenReady() {
    if (isReady()) return Promise.resolve();

    return new Promise((resolve, reject) => {
        const deadline = Date.now() + READY_TIMEOUT_MS;
        const timer = setInterval(() => {
            if (isReady()) {
                clearInterval(timer);
                resolve();
            } else if (Date.now() > deadline) {
                clearInterval(timer);
                reject(new Error('LForms did not finish loading. Check that the LHC-Forms scripts are reachable.'));
            }
        }, POLL_INTERVAL_MS);
    });
}

// Load a Questionnaire, optionally with an existing QuestionnaireResponse.
export async function loadData(questionnaireJson, responseJson, componentId, asReadonly) {
    await whenReady();

    const qData = JSON.parse(questionnaireJson);
    const qrData = responseJson ? JSON.parse(responseJson) : null;

    LForms.Util.addFormToPage(qData, componentId, {
        fhirVersion: 'R4',
        questionnaireResponse: qrData,
        readonlyMode: asReadonly
    });
}

// Read what the user entered back out as a QuestionnaireResponse.
export function getData(componentId, options) {
    if (!isReady()) throw new Error('LForms is not loaded.');
    const response = LForms.Util.getFormFHIRData('QuestionnaireResponse', 'R4', componentId, options);
    return JSON.stringify(response, null, 2);
}
