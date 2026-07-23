"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
const template = {
    properties: {
        'TRoot.$$Tab': String,
        'TRoot.AgentId': { get: agentGetter, set: agentSetter },
        'TRoot.AgentId2': agentGetter
    }
};
exports.default = template;
function agentGetter() {
    let ctrl = this.$ctrl;
    ctrl.$invoke('/test2/test3');
}
function agentSetter(value) {
}
