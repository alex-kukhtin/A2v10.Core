
const template: Template = {
	properties: {
		//'TRoot.$$Default': { type: Number, value: 22 },
		'TRoot.$$Tab': String,
		'TRoot.AgentId': { get: agentGetter, set: agentSetter },
		'TRoot.AgentId2': agentGetter
	}
}

export default template;	

function agentGetter() {

}

function agentSetter(value) {

}