(function (platform$3, vue) {
    'use strict';

    // What the XAML components take from the platform (a2v10-core.js, loaded before): the vocabulary they register into,
    // app.js's registries (app.components: the bases the applications extend) and the std:* modules, untyped, as the
    // Net48 components took them.
    const app = window.app;
    function require$1(name) {
        return platform$3.require(name);
    }

    // v-focus, v-settabindex (Net48 directives/focus.js), v-disable (directives/disable.js), unchanged in behaviour.
    // Vue 2's bind is created, inserted is mounted.
    // selects all text on focus
    platform$3.vocabulary.directive('focus', {
        created(el) {
            function doSelect(t) {
                if (!t.select)
                    return;
                if (t._selectDone)
                    return;
                t.select();
                t._selectDone = true;
            }
            el.addEventListener('focus', function (event) {
                // focus occurs before click!
                const target = event.target;
                target.parentElement.classList.add('focus');
                if (el.__opts && el.__opts.mask)
                    return;
                target._selectDone = false;
                setTimeout(() => {
                    doSelect(target);
                }, 0);
            }, false);
            el.addEventListener('blur', function (event) {
                const t = event.target;
                t._selectDone = true;
                t.parentElement.classList.remove('focus');
            }, false);
            el.addEventListener('click', function (event) {
                if (el.__opts && el.__opts.mask)
                    return;
                doSelect(event.target);
            }, false);
        },
        mounted(el) {
            el._selectDone = false;
            if (el.tabIndex === 1) {
                setTimeout(() => {
                    if (el.focus)
                        el.focus();
                    if (el.select)
                        el.select();
                }, 0);
            }
        }
    });
    // v-disable (Net48 directives/disable.js): a click on a disabled element stops in the capture phase
    platform$3.vocabulary.directive('disable', {
        created(el) {
            function doDisable(event) {
                if (this.getAttribute('disabled')) {
                    event.preventDefault();
                    event.stopImmediatePropagation();
                    return false;
                }
            }
            // with capture !!!
            el.addEventListener('click', doDisable, true);
        }
    });
    platform$3.vocabulary.directive('settabindex', {
        mounted(el) {
            if (el.tabIndex === 1) {
                setTimeout(() => {
                    if (el.focus)
                        el.focus();
                }, 0);
            }
        }
    });

    // a2-pager (Net48 components/pager.js), unchanged in behaviour: the pages of a collection view's source
    // (Parent.Pager: pageSize, offset, sourceCount, $setOffset). Vue 3's h takes flat props: on.click is onClick,
    // domProps and attrs are props.
    const locale$a = window.$$locale;
    const eventBus$c = require$1('std:eventBus');
    const fmt = new Intl.NumberFormat('uk-UA').format;
    platform$3.vocabulary.component('a2-pager', vue.defineComponent({
        props: {
            source: Object,
            emptyText: String,
            templateText: String
        },
        computed: {
            src() {
                return this.source;
            },
            pages() {
                return Math.ceil(this.count / this.src.pageSize);
            },
            currentPage() {
                return Math.ceil(this.offset / this.src.pageSize) + 1;
            },
            title() {
                if (!this.count)
                    return this.emptyString;
                return this.textString;
            },
            emptyString() {
                return this.emptyText ? this.emptyText : locale$a.$NoElements;
            },
            textString() {
                const lastNo = Math.min(this.count, this.offset + this.src.pageSize);
                if (this.templateText)
                    return this.templateText
                        .replace(/\#\[Start\]/g, fmt(this.offset + 1))
                        .replace(/\#\[End\]/g, fmt(lastNo))
                        .replace(/\#\[Count\]/g, fmt(this.count));
                else
                    return `${locale$a.$PagerElements}: <b>${fmt(this.offset + 1)}</b>-<b>${fmt(lastNo)}</b> ${locale$a.$Of} <b>${fmt(this.count)}</b>`;
            },
            offset() {
                return +this.src.offset;
            },
            count() {
                return +this.src.sourceCount;
            }
        },
        methods: {
            setOffset(offset) {
                offset = +offset;
                if (this.offset === offset)
                    return;
                eventBus$c.$emit('closeAllPopups');
                this.$nextTick(() => this.src.$setOffset(offset));
            },
            isActive(page) {
                return page === this.currentPage;
            },
            click(arg, $ev) {
                $ev.preventDefault();
                switch (arg) {
                    case 'prev':
                        this.setOffset(this.offset - this.src.pageSize);
                        break;
                    case 'next':
                        this.setOffset(this.offset + this.src.pageSize);
                        break;
                }
            },
            goto(page, $ev) {
                $ev.preventDefault();
                const offset = (page - 1) * this.src.pageSize;
                this.setOffset(offset);
            }
        },
        render() {
            if (this.src.pageSize === -1)
                return null; // invisible
            const children = [];
            const dotsClass = { class: 'a2-pager-dots' };
            const renderBtn = (page) => {
                return vue.h('button', {
                    innerText: page,
                    onClick: ($ev) => this.goto(page, $ev),
                    class: { active: this.isActive(page) }
                });
            };
            // prev
            children.push(vue.h('button', {
                onClick: ($ev) => this.click('prev', $ev),
                disabled: this.offset === 0, 'aria-label': 'Previous page'
            }, [vue.h('i', { class: 'ico ico-chevron-left' })]));
            // first
            if (this.pages > 0)
                children.push(renderBtn(1));
            // middle
            let ms = 2;
            let me = this.pages - 1;
            if (this.pages == 2)
                me = 2;
            let sd = false, ed = false;
            const cp = this.currentPage;
            const len = me - ms;
            if (len > 4) {
                if (cp > 4)
                    sd = true;
                if (cp < this.pages - 3)
                    ed = true;
                if (sd && !ed)
                    ms = me - 3;
                else if (!sd && ed)
                    me = ms + 3;
                else if (sd && ed) {
                    ms = cp - 1;
                    me = cp + 1;
                }
            }
            if (sd)
                children.push(vue.h('span', dotsClass, '...'));
            for (let mi = ms; mi <= me; ++mi)
                children.push(renderBtn(mi));
            if (ed)
                children.push(vue.h('span', dotsClass, '...'));
            // last
            if (this.pages > 2)
                children.push(renderBtn(this.pages));
            // next
            children.push(vue.h('button', {
                onClick: ($ev) => this.click('next', $ev),
                disabled: this.currentPage >= this.pages, 'aria-label': 'Next Page'
            }, [vue.h('i', { class: 'ico ico-chevron-right' })]));
            children.push(vue.h('span', { class: 'a2-pager-divider' }));
            children.push(vue.h('span', { class: 'a2-pager-title', innerHTML: this.title }));
            return vue.h('div', { class: 'a2-pager' }, children);
        }
    }));

    // validator, a2-static-validator (Net48 components/validator.js), unchanged in behaviour: the errors of a control's
    // value, and of a model property alone.
    platform$3.vocabulary.component('validator', vue.defineComponent({
        props: {
            invalid: Function,
            errors: Array,
            options: Object
        },
        template: '<div v-if="invalid()" class="validator" :class="cssClass" :style="cssStyle"><span v-for="err in errors" v-text="err.msg" :class="err.severity"></span></div>',
        computed: {
            cssStyle() {
                const r = {};
                if (this.options && this.options.width)
                    r.width = this.options.width;
                return r;
            },
            cssClass() {
                const r = {};
                if (!this.options)
                    return r;
                if (this.options.placement)
                    r[this.options.placement] = true;
                if (this.options.width)
                    r['with-width'] = true;
                return r;
            }
        }
    }));
    platform$3.vocabulary.component('a2-static-validator', vue.defineComponent({
        props: {
            item: {
                type: Object, default() {
                    return {};
                }
            },
            prop: String
        },
        template: '<div v-if="invalid()" class="static-validator"><span v-for="err in errors" v-text="err.msg" :class="err.severity"></span></div>',
        computed: {
            path() {
                return this.item._path_ + '.' + this.prop;
            },
            modelValue() {
                if (!this.item)
                    return null;
                return this.item[this.prop];
            },
            errors() {
                const item = this.item;
                if (!item)
                    return null;
                const root = item._root_;
                if (!root)
                    return null;
                if (!root._validate_)
                    return null;
                return root._validate_(item, this.path, this.modelValue, this.deferUpdate);
            }
        },
        methods: {
            invalid() {
                // method! no cache!
                const err = this.errors;
                if (!err)
                    return false;
                return err.length > 0;
            }
        }
    }));

    // control (Net48 components/control.js), unchanged in behaviour: the base of the input controls (textbox, combobox,
    // datepicker…): label, validation, the masked or formatted value, the accelerator. Not a tag: app.components['control'],
    // which the controls and the applications extend. The item and prop are the derived control's props.
    // Vue 3: beforeDestroy is beforeUnmount; $children is gone (deferUpdate: forceUpdateChildren). itemToValidate and
    // propToValidate are a derived control's, when it declares them: read from $props, where an undeclared one is
    // undefined as in Vue 2, not through the instance, where Vue 3 warns while it renders.
    const utils$g = require$1('std:utils');
    const mask$1 = require$1('std:mask');
    const maccel = require$1('std:accel');
    // Vue 2's $children: the component instances of a component's own render, not deeper. Each is updated.
    function forceUpdateChildren(instance) {
        const walk = (vnode) => {
            if (!vnode)
                return;
            if (vnode.component) {
                vnode.component.proxy.$forceUpdate();
                return;
            }
            if (Array.isArray(vnode.children))
                for (const c of vnode.children)
                    walk(c);
        };
        walk(instance.subTree);
    }
    const control = vue.defineComponent({
        props: {
            label: String,
            required: { type: null },
            align: { type: String, default: 'left' },
            description: String,
            disabled: { type: null },
            tabIndex: Number,
            dataType: String,
            format: String,
            validatorOptions: Object,
            updateTrigger: String,
            mask: String,
            hideZeros: Boolean,
            testId: String,
            accel: String,
            highlight: { type: null }
        },
        computed: {
            path() {
                const self = this;
                if (self.item._path_)
                    return self.item._path_ + '.' + self.prop;
                else
                    return self.prop;
            },
            pathToValidate() {
                const self = this;
                const p = self.$props;
                return p.itemToValidate._path_ + '.' + p.propToValidate;
            },
            modelValue() {
                const self = this;
                if (!self.item)
                    return null;
                const val = self.item[self.prop];
                if (this.dataType)
                    return utils$g.format(val, this.dataType, { hideZeros: this.hideZeros, format: this.format });
                else if (this.mask && val)
                    return mask$1.getMasked(this.mask, val);
                return val;
            },
            hasValue() {
                const self = this;
                if (!self.item)
                    return false;
                const val = self.item[self.prop];
                if (!val)
                    return false;
                if (utils$g.isObjectExact(val))
                    return !!val.Id;
                return true;
            },
            modelValueRaw() {
                const self = this;
                if (!self.item)
                    return null;
                return self.item[self.prop];
            },
            errors() {
                const self = this;
                if (!self.item)
                    return null;
                const root = self.item._root_;
                if (!root)
                    return null;
                if (!root._validate_)
                    return null;
                let err;
                const p = self.$props;
                if (p.itemToValidate)
                    err = root._validate_(p.itemToValidate, this.pathToValidate, p.itemToValidate[p.propToValidate], this.deferUpdate);
                else
                    err = root._validate_(self.item, this.path, this.modelValueRaw, this.deferUpdate);
                return err;
            },
            inputClass() {
                let cls = '';
                if (this.align && this.align !== 'left')
                    cls += 'text-' + this.align;
                if (this.isNegative)
                    cls += ' negative-red';
                if (this.updateTrigger === 'input')
                    cls += ' trigger-input';
                return cls;
            },
            isNegative() {
                const self = this;
                if (this.dataType === 'Number' || this.dataType === 'Currency')
                    if (self.item && this.modelValue < 0)
                        return true;
                return false;
            },
            hasLabel() {
                return !!this.label;
            },
            hasDescr() {
                return !!this.description;
            },
            maxLength() {
                const self = this;
                if (!self.item)
                    return undefined;
                if (!self.item.$maxLength)
                    return undefined;
                return self.item.$maxLength(self.prop);
            }
        },
        mounted() {
            const self = this;
            // direct parent only
            if (self.$parent.$registerControl)
                self.$parent.$registerControl(this);
            if (this.accel)
                self._accelKey = maccel.registerControl(this.accel, this.$refs.input, 'focus');
            if (!this.mask)
                return;
            mask$1.mountElement(this.$refs.input, this.mask);
        },
        beforeUnmount() {
            const self = this;
            // direct parent only
            if (self.$parent.$unregisterControl)
                self.$parent.$unregisterControl(this);
            if (this.accel)
                maccel.unregisterControl(self._accelKey);
            if (!this.mask)
                return;
            mask$1.unmountElement(this.$refs.input, this.mask);
        },
        methods: {
            valid() {
                // method! no cache!
                return !this.invalid();
            },
            invalid(out) {
                // method! no cache!
                const err = this.errors;
                if (!err)
                    return false;
                if (out) {
                    out.warn = err.every((r) => r.severity === 'warning');
                    out.info = err.every((r) => r.severity === 'info');
                    out.all = err.length;
                }
                return err.length > 0;
            },
            pending() {
                return this.errors && this.errors.pending > 0;
            },
            cssClass() {
                // method! no cached!!!
                const out = {};
                const inv = this.invalid(out);
                let cls = 'control-group' + (inv ? ' invalid' : ' valid');
                if (inv && out.warn)
                    cls += ' val-warning';
                else if (inv && out.info)
                    cls += ' val-info';
                if (this.required)
                    cls += ' required';
                if (this.disabled)
                    cls += ' disabled';
                if (this.highlight && this.hasValue)
                    cls += ' has-value';
                return cls;
            },
            deferUpdate() {
                forceUpdateChildren(this.$);
                this.$forceUpdate();
            },
            test() {
                alert('from base control');
            }
        },
        watch: {
            mask() {
                const self = this;
                mask$1.setMask(this.$refs.input, this.mask);
                if (self.updateValue)
                    self.updateValue(this.$refs.input.value);
            }
        }
    });
    app.components['control'] = control;

    // textbox, a2-textarea, static (Net48 components/textbox.js), unchanged in behaviour: over control (control.ts).
    // Vue 3: an event a component does not declare in emits is also a native listener on its root, and the input's own
    // change bubbles to it: the handler would run twice. change is declared.
    // password-- fake fields are a workaround for chrome autofill getting the wrong fields
    const utils$f = require$1('std:utils');
    const mask = require$1('std:mask');
    const textBoxTemplate = `<div :class="cssClass()" :test-id="testId">
	<label v-if="hasLabel"><span v-text="label"/><slot name="hint"/><slot name="link"></slot></label>
	<div class="input-group">
		<input v-if="password" type="text" class="fake-pwd-field" tabindex="-1"/>
		<input ref="input" :type="controlType" v-focus :autocomplete="autocompleteText"
			v-bind:value="modelValue"
				v-on:change="onChange($event.target.value)"
				v-on:input="onInput($event.target.value)"
				v-on:keypress="onKey($event)"
				:class="inputClass" :placeholder="placeholder" :readonly="disabled" :tabindex="tabIndex" :maxlength="maxLength" :spellcheck="spellCheck"/>
		<slot></slot>
		<a class="a2-hyperlink add-on a2-inline" href="" @click.stop.prevent="dummy" v-if=hasFilter><i class="ico ico-filter-outline"></i></a>
		<a class="a2-hyperlink add-on a2-inline" href="" @click.stop.prevent="dummy" v-if=searchVisible><i class="ico ico-search"></i></a>
		<a class="a2-hyperlink add-on a2-inline" tabindex="-1" href="" @click.stop.prevent="clear" v-if="clearVisible"><i class="ico ico-clear"></i></a>
		<validator :invalid="invalid" :errors="errors" :options="validatorOptions"></validator>
	</div>
	<slot name="popover"></slot>
	<span class="descr" v-if="hasDescr" v-text="description"></span>
</div>
`;
    const textAreaTemplate = `<div :class="cssClass()" :test-id="testId">
	<label v-if="hasLabel"><span v-text="label"/><slot name="hint"/><slot name="link"></slot></label>
	<div class="input-group">
		<textarea ref="input" v-focus v-auto-size="autoSize" v-bind:value="modelValue2" :style="areaStyle"
			v-on:change="onChange($event.target.value)"
			v-on:input="onInput($event.target.value)"
			v-on:keypress="onKey($event)"
			:rows="rows" :class="inputClass" :placeholder="placeholder" :readonly="disabled" :tabindex="tabIndex" :maxlength="maxLength" :spellcheck="spellCheck"/>
		<slot></slot>
		<validator :invalid="invalid" :errors="errors" :options="validatorOptions"></validator>
	</div>
	<slot name="popover"></slot>
	<span class="descr" v-if="hasDescr" v-text="description"></span>
</div>
`;
    const staticTemplate = `<div :class="cssClass()" :test-id="testId">
	<label v-if="hasLabel"><span v-text="label"/><slot name="hint"/><slot name="link"></slot></label>
	<div class="input-group static">
		<span v-focus v-text="textProp" :class="inputClass" :tabindex="tabIndex" class="static-input"/>
		<slot></slot>
		<validator :invalid="invalid" :errors="errors" :options="validatorOptions"></validator>
	</div>
	<slot name="popover"></slot>
	<span class="descr" v-if="hasDescr" v-text="description"></span>
</div>
`;
    const textbox = vue.defineComponent({
        extends: control,
        template: textBoxTemplate,
        emits: ['change'],
        props: {
            item: {
                type: [Object, Array], default() {
                    return {};
                }
            },
            prop: String,
            itemToValidate: Object,
            propToValidate: String,
            placeholder: String,
            password: Boolean,
            number: Boolean,
            spellCheck: { type: Boolean, default: undefined },
            enterCommand: Function,
            hasClear: Boolean,
            hasFilter: Boolean,
            hasSearch: Boolean,
            filters: Array
        },
        computed: {
            controlType() {
                return this.password ? 'password' : 'text';
            },
            autocompleteText() {
                return this.password ? 'new-password' : 'off';
            },
            searchVisible() {
                if (this.clearVisible)
                    return false;
                return this.hasSearch;
            },
            clearVisible() {
                if (!this.hasClear)
                    return false;
                return !!this.item[this.prop];
            }
        },
        methods: {
            doFilter(value) {
                return utils$f.text.applyFilters(this.filters, value);
            },
            updateValue(value) {
                const item = this.item;
                if (this.mask)
                    item[this.prop] = mask.getUnmasked(this.mask, this.doFilter(value));
                else
                    item[this.prop] = utils$f.parse(this.doFilter(value), this.dataType);
                const mv = this.modelValue;
                const input = this.$refs.input;
                if (input.value !== mv) {
                    input.value = mv;
                    this.$emit('change', item[this.prop]);
                }
            },
            onInput(value) {
                if (this.updateTrigger === 'input')
                    this.updateValue(value);
            },
            onChange(value) {
                if (this.updateTrigger !== 'input')
                    this.updateValue(value);
            },
            onKey(event) {
                if (this.number) {
                    if ((event.charCode < 48 || event.charCode > 57) && event.charCode !== 45 /*minus*/) {
                        event.preventDefault();
                        event.stopPropagation();
                    }
                }
                if (event.code === 'Enter') {
                    if (this.enterCommand) {
                        this.enterCommand();
                    }
                }
            },
            clear() {
                this.item[this.prop] = '';
            },
            dummy() {
            }
        }
    });
    const textarea = vue.defineComponent({
        extends: control,
        template: textAreaTemplate,
        emits: ['change'],
        props: {
            item: {
                type: [Object, Array], default() {
                    return {};
                }
            },
            prop: String,
            itemToValidate: Object,
            propToValidate: String,
            placeholder: String,
            autoSize: Boolean,
            rows: Number,
            spellCheck: { type: Boolean, default: undefined },
            enterCommand: Function,
            maxHeight: String,
            filters: Array
        },
        computed: {
            modelValue2() {
                if (!this.item)
                    return null;
                return this.item[this.prop];
            },
            areaStyle() {
                if (this.maxHeight)
                    return { 'max-height': this.maxHeight };
                return undefined;
            }
        },
        methods: {
            updateValue(value) {
                const item = this.item;
                value = utils$f.text.applyFilters(this.filters, value);
                if (item[this.prop] === value)
                    return;
                item[this.prop] = value;
                this.$emit('change', item[this.prop]);
            },
            onInput(value) {
                if (this.updateTrigger === 'input')
                    this.updateValue(value);
            },
            onChange(value) {
                if (this.updateTrigger !== 'input')
                    this.updateValue(value);
            },
            onKey(event) {
                if (event.code === 'Enter' && event.ctrlKey) {
                    if (this.enterCommand) {
                        this.enterCommand();
                    }
                }
            }
        },
        watch: {
            modelValue() {
                if (!this.autoSize)
                    return;
                const inp = this.$refs.input;
                if (!inp)
                    return;
                // send for auto size
                const evt = new Event('autosize', { bubbles: false, cancelable: true });
                inp.dispatchEvent(evt);
            }
        }
    });
    const staticControl = vue.defineComponent({
        extends: control,
        template: staticTemplate,
        props: {
            item: {
                type: [Object, Array], default() {
                    return {};
                }
            },
            prop: String,
            itemToValidate: Object,
            propToValidate: String,
            text: [String, Number, Date]
        },
        computed: {
            textProp() {
                if (this.mask && this.text)
                    return mask.getMasked(this.mask, this.text);
                return this.text;
            }
        }
    });
    platform$3.vocabulary.component('textbox', textbox);
    app.components['textbox'] = textbox;
    // Net48 meant app.components for these two too, but wrote app.components['a2-textarea', textarea]: none.
    platform$3.vocabulary.component('a2-textarea', textarea);
    platform$3.vocabulary.component('static', staticControl);

    // collection-view-server, collection-view-server-url (Net48 components/collectionviewplain.js), unchanged in behaviour:
    // the pager, the sort and the filter of a server's array (its $ModelInfo); a change reloads the page's data. The
    // client collection-view of the same file is not ported yet.
    // Vue 3:
    // - the prop is itemsSource: Vue 3 matches items-source to itemsSource only (Vue 2 also to ItemsSource); the code
    //   reads ItemsSource, a computed;
    // - Vue 2's $root was the page (base.js): $root.$emit('cwChange') and $root[prop] are the page's controller
    //   (inject $page, page.ts): __cwChange, $data;
    // - $on('sort') is gone: doSort is called by the data grid (the data-grid step).
    const utils$e = require$1('std:utils');
    const period = require$1('std:period');
    const eventBus$b = require$1('std:eventBus');
    const eqlower$1 = utils$e.text.equalNoCase;
    function getModelInfoProp(src, propName) {
        if (!src)
            return undefined;
        const mi = src.$ModelInfo;
        if (!mi)
            return undefined;
        return mi[propName];
    }
    function setModelInfoProp(src, propName, value) {
        if (!src)
            return;
        const mi = src.$ModelInfo;
        if (!mi)
            return;
        mi[propName] = value;
    }
    function modelInfoToFilter(q, filter) {
        if (!q)
            return;
        for (const x in filter) {
            if (x in q) {
                const iv = filter[x];
                if (period.isPeriod(iv)) {
                    filter[x] = iv.fromUrl(q[x]);
                }
                else if (utils$e.isDate(iv)) {
                    filter[x] = utils$e.date.tryParse(q[x]);
                }
                else if (utils$e.isObjectExact(iv))
                    iv.Id = q[x];
                else if (utils$e.isNumber(iv))
                    filter[x] = +q[x];
                else {
                    filter[x] = q[x];
                }
            }
        }
    }
    // server collection view
    const collectionView = vue.defineComponent({
        template: `
<div>
	<slot :ItemsSource="ItemsSource" :Pager="thisPager" :Filter="filter" :ParentCollectionView="parentCw">
	</slot>
</div>
`,
        inject: {
            page: { from: '$page', default: null }
        },
        props: {
            itemsSource: [Array, Object],
            initialFilter: Object,
            persistentFilter: Array
        },
        data() {
            return {
                filter: this.initialFilter,
                lockChange: true
            };
        },
        watch: {
            jsonFilter: {
                handler() {
                    this.filterChanged();
                }
            },
            ItemsSource: {
                handler() {
                    this.updateFilter();
                }
            }
        },
        computed: {
            ItemsSource() {
                return this.itemsSource;
            },
            jsonFilter() {
                return utils$e.toJson(this.filter);
            },
            thisPager() {
                return this;
            },
            pageSize() {
                return getModelInfoProp(this.ItemsSource, 'PageSize');
            },
            dir() {
                return getModelInfoProp(this.ItemsSource, 'SortDir');
            },
            order() {
                return getModelInfoProp(this.ItemsSource, 'SortOrder');
            },
            offset() {
                return getModelInfoProp(this.ItemsSource, 'Offset');
            },
            pages() {
                const cnt = this.sourceCount;
                return Math.ceil(cnt / this.pageSize);
            },
            sourceCount() {
                if (!this.ItemsSource)
                    return 0;
                return this.ItemsSource.$RowCount || 0;
            },
            parentCw() {
                // find parent collection view; none: the page (Vue 2: the page's root instance)
                let p = this.$parent;
                while (p && p.$.type !== collectionView)
                    p = p.$parent;
                return p || this.page;
            }
        },
        methods: {
            $setOffset(offset) {
                if (this.offset === offset)
                    return;
                setModelInfoProp(this.ItemsSource, 'Offset', offset);
                this.reload();
            },
            sortDir(order) {
                return eqlower$1(order, this.order) ? this.dir : undefined;
            },
            doSort(order) {
                if (eqlower$1(order, this.order)) {
                    const dir = eqlower$1(this.dir, 'asc') ? 'desc' : 'asc';
                    setModelInfoProp(this.ItemsSource, 'SortDir', dir);
                }
                else {
                    setModelInfoProp(this.ItemsSource, 'SortOrder', order);
                    setModelInfoProp(this.ItemsSource, 'SortDir', 'asc');
                }
                this.reload();
            },
            filterChanged() {
                if (this.lockChange)
                    return;
                let mi = this.ItemsSource.$ModelInfo;
                if (!mi) {
                    mi = { Filter: this.filter };
                    this.ItemsSource.$ModelInfo = mi;
                }
                else {
                    this.ItemsSource.$ModelInfo.Filter = this.filter;
                }
                if (this.persistentFilter && this.persistentFilter.length) {
                    const parentProp = this.ItemsSource._path_;
                    const propIx = parentProp.lastIndexOf('.');
                    const propTop = parentProp.indexOf('[]');
                    const lastProp = parentProp.substring(propIx + 1);
                    const firstProp = parentProp.substring(0, propTop);
                    for (const topElem of this.page.$data[firstProp].$allItems()) {
                        if (!topElem[lastProp].$ModelInfo)
                            topElem[lastProp].$ModelInfo = mi;
                        else {
                            for (const pp of this.persistentFilter) {
                                const te = topElem[lastProp];
                                if (!utils$e.isEqual(te.$ModelInfo.Filter[pp], this.filter[pp])) {
                                    te.$ModelInfo.Filter[pp] = this.filter[pp];
                                    if (te.$loaded)
                                        te.$empty();
                                    te.$loaded = false;
                                }
                            }
                        }
                    }
                }
                if ('Offset' in mi)
                    setModelInfoProp(this.ItemsSource, 'Offset', 0);
                this.reload();
            },
            reload() {
                this.page.__cwChange(this.ItemsSource);
            },
            updateFilter() {
                // modelInfo to filter
                const mi = this.ItemsSource ? this.ItemsSource.$ModelInfo : null;
                if (!mi)
                    return;
                const fi = mi.Filter;
                if (!fi)
                    return;
                this.lockChange = true;
                for (const prop in this.filter) {
                    if (prop in fi)
                        this.filter[prop] = fi[prop];
                }
                this.$nextTick(() => {
                    this.lockChange = false;
                });
            },
            __setFilter(props) {
                if (this.ItemsSource !== props.source)
                    return;
                if (period.isPeriod(props.value))
                    this.filter[props.prop].assign(props.value);
                else
                    this.filter[props.prop] = props.value;
            }
        },
        created() {
            // get filter values from modelInfo
            const mi = this.ItemsSource ? this.ItemsSource.$ModelInfo : null;
            if (mi) {
                modelInfoToFilter(mi.Filter, this.filter);
            }
            this.$nextTick(() => {
                this.lockChange = false;
            });
            eventBus$b.$on('setFilter', this.__setFilter);
        },
        beforeUnmount() {
            eventBus$b.$off('setFilter', this.__setFilter);
        }
    });
    platform$3.vocabulary.component('collection-view-server', collectionView);
    platform$3.vocabulary.component('collection-view-server-url', collectionView);

    // v-lazy (Net48 directives/lazy.js), unchanged in behaviour: a lazy array bound to an element loads itself when the
    // element is mounted and when it updates. Vue 2's inserted is mounted, componentUpdated is updated.
    function updateLazy(arr) {
        if (arr && arr.$loadLazy) {
            arr.$loadLazy();
        }
    }
    platform$3.vocabulary.directive('lazy', {
        updated(_el, binding) {
            updateLazy(binding.value);
        },
        mounted(_el, binding) {
            updateLazy(binding.value);
        }
    });

    // data-grid, data-grid-column (Net48 components/datagrid.js), unchanged in behaviour: columns register with their grid
    // ($parent.$addColumn), rows and cells render the grid's items, grouped or not, sorted by the collection view or
    // locally. Some ideas from https://github.com/andrewcourtice/vuetiful/tree/master/src/components/datatable
    // Groupings: "v-show" on a line is much faster than "v-if" on an entire template.
    // Vue 3:
    // - the cell is a functional component: a function of its props; h takes flat props;
    // - the cell's validator and command link were component objects made at each render; Vue 2 cached their compiled
    //   templates by text, this compiler keeps no cache: they are made once, the command's arguments are props;
    // - <template v-for> carries the key (Vue 3 refuses it on the children); <col> is void;
    // - a ref in v-for is an array in no promised order: the selected row is found by its index;
    // - $parent.$emit('sort') was heard by the collection view's $on('sort', doSort): $parent.doSort;
    // - $scopedSlots is $slots; Vue.set is an assignment; $root ($data, inDialog) is the page's controller (inject).
    const utils$d = require$1('std:utils');
    const log = require$1('std:log');
    const eventBus$a = require$1('std:eventBus');
    const locale$9 = window.$$locale;
    const eqlower = utils$d.text.equalNoCase;
    const dataGridTemplate = `
<div v-lazy="itemsSource" :class="{'data-grid-container':true, 'fixed-header': fixedHeader, 'bordered': border, 'compact': compact}" :test-id="testId">
	<div class="data-grid-header-border" v-if="fixedHeader" />
	<div :class="{'data-grid-body': true, 'fixed-header': fixedHeader}">
	<div class="data-grid-empty" v-if="$isEmpty">
		<slot name="empty" />
	</div>
	<table :class="cssClass">
		<colgroup>
			<col v-if="isMarkCell" class="fit"/>
			<col v-if="isRowDetailsCell" class="fit" />
			<col v-bind:class="columnClass(col, colIndex)" v-bind:style="columnStyle(col)" v-for="(col, colIndex) in columns" :key="colIndex"/>
		</colgroup>
		<thead>
			<tr v-show="isHeaderVisible">
				<th v-if="isMarkCell" class="marker"><div v-if=fixedHeader class="h-fill"></div><div v-if=fixedHeader class="h-holder">&#160;</div></th>
				<th v-if="isRowDetailsCell" class="details-marker"><div v-if="fixedHeader" class="h-holder">&#160;</div></th>
				<slot></slot>
			</tr>
		</thead>
		<template v-if="isGrouping">
			<tbody>
				<template v-for="(g, gIndex) of $groups" :key="gIndex">
					<tr v-if="isGroupGroupVisible(g)" :class="'group lev-' + g.level">
						<td @click.prevent='toggleGroup(g)' :colspan="groupColumns">
						<span :class="{expmark: true, expanded: g.expanded}" />
						<span class="grtitle" v-text="groupTitle(g)" />
						<span v-if="isGroupCountVisible(g)" class="grcount" v-text="g.count" /></td>
					</tr>
					<template v-for="(row, rowIndex) in g.items" :key="gIndex + ':' + rowIndex">
						<data-grid-row v-show="isGroupBodyVisible(g)" :group="true" :level="g.level" :cols="columns" :row="row" :index="rowIndex" :mark="mark" ref="row"  :is-item-active="isItemActive" :hit-item="hitItem"/>
						<data-grid-row-details v-if="rowDetailsShow(row)" v-show="isGroupBodyVisible(g)" :cols="columns.length" :row="row" :mark="mark">
							<slot name="row-details" :row="row"></slot>
						</data-grid-row-details>
					</template>
				</template>
			</tbody>
		</template>
		<template v-else>
			<tbody>
				<template v-for="(item, rowIndex) in $items" :key="rowIndex">
					<data-grid-row :cols="columns" :row="item" :index="rowIndex" :mark="mark" ref="row" :is-item-active="isItemActive" :hit-item="hitItem"/>
					<data-grid-row-details v-if="rowDetailsShow(item)" :cols="columns.length" :row="item" :mark="mark">
						<slot name="row-details" :row="item"></slot>
					</data-grid-row-details>
				</template>
			</tbody>
		</template>
		<slot name="footer"></slot>
	</table>
	</div>
</div>
`;
    /* @click.prevent disables checkboxes & other controls in cells
    <td class="group-marker" v-if="group"></td>
    */
    const dataGridRowTemplate = `
<tr @click="rowSelect(row)" :class="rowClass()" v-on:dblclick.prevent="doDblClick" ref="tr" @mousedown="mouseDown(row)">
	<td v-if="isMarkCell" class="marker">
		<div :class="markClass"></div>
	</td>
	<td v-if="detailsMarker" class="details-marker" @click.prevent="toggleDetails">
		<i v-if="detailsIcon" class="ico" :class="detailsExpandClass" />
	</td>
	<data-grid-cell v-for="(col, colIndex) in cols" :key="colIndex" :row="row" :col="col" :index="index" />
</tr>`;
    const dataGridRowDetailsTemplate = `
<tr v-if="visible()" class="row-details">
	<td v-if="isMarkCell" class="marker">
		<div :class="markClass"></div>
	</td>
	<td :colspan='totalCols' class="details-cell">
		<div class="details-wrapper"><slot></slot></div>
	</td>
</tr>
`;
    /**
        icon on header!!!
        <i :class="\'ico ico-\' + icon" v-if="icon"></i>
     */
    const dataGridColumnTemplate = `
<th :class="cssClass" @click.prevent="doSort">
	<div class="h-fill" v-if="fixedHeader" v-text="headerText">
	</div>
	<div class="h-holder">
		<slot><label v-if=checkAll class="like-checkbox" :class="checkAllClass" @click=doCheckAll><span></span></label>
			<span v-else v-text="headerText"></span>
		</slot>
	</div>
</th>
`;
    const dataGridColumn = vue.defineComponent({
        name: 'data-grid-column',
        template: dataGridColumnTemplate,
        inject: {
            page: { from: '$page', default: null }
        },
        props: {
            header: String,
            content: String,
            dataType: String,
            hideZeros: Boolean,
            format: String,
            icon: String,
            bindIcon: String,
            id: String,
            align: { type: String, default: 'left' },
            editable: { type: Boolean, default: false },
            noPadding: { type: Boolean, default: false },
            validate: String,
            sort: { type: Boolean, default: undefined },
            sortProp: String,
            small: { type: Boolean, default: undefined },
            bold: String, //{ type: Boolean, default: undefined },
            mark: String,
            done: String,
            controlType: String,
            width: String,
            minWidth: String,
            fit: Boolean,
            backColor: String,
            wrap: String,
            command: Object,
            maxChars: Number,
            lineClamp: Number,
            colCssClass: String,
            checkAll: String
        },
        created() {
            this.$parent.$addColumn(this);
        },
        unmounted() {
            this.$parent.$removeColumn(this);
        },
        computed: {
            grid() {
                return this.$parent;
            },
            sortProperty() {
                return this.sortProp || this.content;
            },
            dir() {
                return this.grid.sortDir(this.sortProperty);
            },
            fixedHeader() {
                return this.grid.fixedHeader;
            },
            isSortable() {
                if (!this.sortProperty)
                    return false;
                return typeof this.sort === 'undefined' ? this.grid.isGridSortable : this.sort;
            },
            isUpdateUrl() {
                return !this.page.inDialog;
            },
            template() {
                return this.id ? this.grid.$slots[this.id] : null;
            },
            classAlign() {
                return this.align !== 'left' ? (' text-' + this.align).toLowerCase() : '';
            },
            checkAllClass() {
                const state = this.grid.$checkAllState(this.checkAll);
                if (state === 1)
                    return 'checked'; // indeterminate';
                else if (state === 2)
                    return 'indeterminate';
                return undefined;
            },
            cssClass() {
                let cssClass = this.classAlign;
                if (this.isSortable) {
                    cssClass += ' sort';
                    if (this.dir)
                        cssClass += ' ' + this.dir;
                }
                if (this.checkAll)
                    cssClass += ' check-all';
                return cssClass;
            },
            headerText() {
                return this.header || '\xa0';
            },
            evalOpts() {
                return {
                    hideZeros: this.hideZeros,
                    format: this.format
                };
            }
        },
        methods: {
            doSort() {
                if (!this.isSortable)
                    return;
                this.grid.doSort(this.sortProperty);
            },
            doCheckAll() {
                if (!this.checkAll)
                    return;
                this.grid.$checkAll(this.checkAll);
            },
            cellCssClass(row, editable) {
                let cssClass = this.classAlign;
                if (this.mark) {
                    const mark = utils$d.simpleEval(row, this.mark);
                    if (mark)
                        cssClass += ' ' + mark;
                }
                if (editable && this.controlType !== 'checkbox')
                    cssClass += ' cell-editable';
                function addClassBool(bind, cls) {
                    if (!bind)
                        return;
                    if (bind === 'true')
                        cssClass += cls;
                    else if (bind.startsWith('{')) {
                        const prop = bind.substring(1, bind.length - 1);
                        if (utils$d.simpleEval(row, prop))
                            cssClass += cls;
                    }
                }
                if (this.wrap)
                    cssClass += ' ' + this.wrap;
                if (this.small)
                    cssClass += ' small';
                addClassBool(this.bold, ' bold');
                if (this.colCssClass)
                    cssClass += ' ' + this.colCssClass;
                return cssClass.trim();
            }
        }
    });
    platform$3.vocabulary.component('data-grid-column', dataGridColumn);
    // what the cell renders by a column's controlType 'validator', its validate, its command
    const cellValid = {
        props: ['item', 'col'],
        template: '<span><i v-if="item.$invalid" class="ico ico-error"></i></span>'
    };
    const cellValidator = {
        props: ['path', 'item'],
        template: '<validator :path="path" :item="item"></validator>'
    };
    // column command -> hyperlink; args: arg1..arg4 as the cell normalized them
    const cellCommand = vue.defineComponent({
        props: ['row', 'col', 'args'],
        /*@click.prevent, no stop*/
        template: '<a @click.prevent="doCommand($event)" :href="getHref()"><i v-if="hasIcon" :class="iconClass" class="ico"></i><span v-text="eval(row, col.content, col.dataType, col.evalOpts)"></span></a>',
        computed: {
            hasIcon() { return this.col.icon || this.col.bindIcon || this.col.done; },
            iconClass() {
                const col = this.col;
                const icoSingle = !col.content ? ' ico-single' : '';
                if (col.bindIcon)
                    return 'ico-' + utils$d.eval(this.row, col.bindIcon) + icoSingle;
                else if (col.done)
                    return (utils$d.eval(this.row, col.done) ? 'ico-success-outline-green' : 'ico-warning-outline-yellow') + icoSingle;
                else if (col.icon)
                    return 'ico-' + col.icon + icoSingle;
                return null;
            }
        },
        methods: {
            doCommand() {
                this.col.command.cmd(...this.args);
            },
            eval: utils$d.eval,
            getHref() {
                const col = this.col;
                if (!col.command)
                    return null;
                if (col.command.isDialog)
                    return '';
                if (col.command.cmd.name.indexOf('$exec') !== -1)
                    return '';
                const [arg1, arg2] = this.args;
                let id = arg2;
                if (utils$d.isObjectExact(arg2))
                    id = arg2.$id;
                return arg1 + '/' + id;
            }
        }
    });
    const dataGridCell = (props) => {
        const tag = 'td';
        const row = props.row;
        const col = props.col;
        const ix = props.index;
        const cellProps = {
            class: col.cellCssClass(row, col.editable || col.noPadding)
        };
        if (col.template) {
            const vNodes = col.template({ row: row, col: col });
            return vue.h(tag, cellProps, vNodes);
        }
        if (col.controlType === 'validator') {
            cellProps.class = { 'cell-validator': true };
            return vue.h(tag, cellProps, [vue.h(cellValid, { item: row, col: col })]);
        }
        if (!col.content && !col.icon && !col.bindIcon && !col.done) {
            return vue.h(tag, cellProps);
        }
        function normalizeArg(arg, doEval) {
            if (utils$d.isBoolean(arg) || utils$d.isNumber(arg) || utils$d.isObjectExact(arg))
                return arg;
            arg = arg || '';
            if (arg === 'this')
                arg = row;
            else if (arg.startsWith('{')) {
                arg = arg.substring(1, arg.length - 1);
                if (arg.indexOf('.') !== -1)
                    arg = utils$d.eval(row, arg);
                else {
                    if (!(arg in row))
                        throw new Error(`Property '${arg}' not found in ${row.constructor.name} object`);
                    arg = row[arg];
                }
            }
            else if (arg && doEval) {
                arg = utils$d.eval(row, arg, col.dataType, col.evalOpts);
            }
            return arg;
        }
        if (col.command) {
            const arg1 = normalizeArg(col.command.arg1, false);
            const arg2 = normalizeArg(col.command.arg2, col.command.eval);
            const arg3 = normalizeArg(col.command.arg3, false);
            const arg4 = col.command.arg4; // without normalize
            return vue.h(tag, cellProps, [vue.h(cellCommand, { row: row, col: col, args: [arg1, arg2, arg3, arg4] })]);
        }
        /* simple content */
        if (col.content === '$index')
            return vue.h(tag, cellProps, [ix + 1]);
        function isNegativeRed(col) {
            if (col.dataType === 'Number' || col.dataType === 'Currency') {
                const val = utils$d.eval(row, col.content, col.dataType, col.evalOpts, true /*skip format*/);
                if (val < 0)
                    return true;
            }
            return false;
        }
        let content = utils$d.eval(row, col.content, col.dataType, col.evalOpts);
        const spanProps = { class: { 'dg-cell': true, 'negative-red': isNegativeRed(col), 'line-clamp': col.lineClamp > 0 } };
        if (col.maxChars) {
            spanProps.title = content;
            content = utils$d.text.maxChars(content, col.maxChars);
        }
        else if (col.lineClamp > 0) {
            spanProps.title = content;
            spanProps.style = { '-webkit-line-clamp': col.lineClamp };
        }
        const chElems = [vue.h('span', spanProps, content)];
        const icoSingle = !col.content ? ' ico-single' : '';
        if (col.icon)
            chElems.unshift(vue.h('i', { class: 'ico ico-' + col.icon + icoSingle }));
        else if (col.bindIcon)
            chElems.unshift(vue.h('i', { class: 'ico ico-' + utils$d.eval(row, col.bindIcon) + icoSingle }));
        else if (col.done)
            chElems.unshift(vue.h('i', { class: 'ico ico-' + (utils$d.eval(row, col.done) ? 'success-outline-green' : 'warning-outline-yellow') + icoSingle }));
        /*TODO: validate ???? */
        if (col.validate) {
            chElems.push(vue.h(cellValidator, { path: col.validate, item: row }));
        }
        return vue.h(tag, cellProps, chElems);
    };
    dataGridCell.props = ['row', 'col', 'index'];
    const dataGridRow = vue.defineComponent({
        name: 'data-grid-row',
        template: dataGridRowTemplate,
        components: {
            'data-grid-cell': dataGridCell
        },
        props: {
            row: Object,
            cols: Array,
            index: Number,
            mark: String,
            group: Boolean,
            level: Number,
            isItemActive: Function,
            hitItem: Function
        },
        computed: {
            grid() {
                return this.$parent;
            },
            isMarkCell() {
                return this.grid.isMarkCell;
            },
            detailsMarker() {
                return this.grid.isRowDetailsCell;
            },
            detailsIcon() {
                if (!this.detailsMarker)
                    return false;
                const prdv = this.grid.rowDetailsVisible;
                if (prdv === false)
                    return true; // property not specified
                return prdv && this.row[prdv];
            },
            detailsExpandClass() {
                return this.row._uiprops_.$details ? 'ico-minus-circle' : 'ico-plus-circle';
            },
            totalColumns() {
                console.error('implement me');
            },
            markClass() {
                return this.mark ? utils$d.simpleEval(this.row, this.mark) : '';
            },
            _itemActive() {
                return this.isItemActive ? this.isItemActive(this.index, this.row) : !!this.row.$selected;
            }
        },
        watch: {
            _itemActive(newValue) {
                if (newValue) {
                    const tr = this.$refs.tr;
                    if (tr && tr.scrollIntoViewCheck)
                        tr.scrollIntoViewCheck();
                }
            }
        },
        methods: {
            rowClass() {
                const row = this.row;
                let cssClass = 'dg-row';
                const isActive = this.isItemActive ? this.isItemActive(this.index, row) : !!row.$selected;
                if (isActive)
                    cssClass += ' active';
                if (this.grid.isMarkRow && this.mark) {
                    cssClass += ' ' + utils$d.simpleEval(row, this.mark);
                }
                if ((this.index + 1) % 2)
                    cssClass += ' even';
                if (this.grid.rowBold && row[this.grid.rowBold])
                    cssClass += ' bold';
                if (this.level)
                    cssClass += ' lev-' + this.level;
                return cssClass.trim();
            },
            rowSelect(row) {
                if (row.$select)
                    row.$select();
            },
            mouseDown(row) {
                if (this.hitItem)
                    this.hitItem(row);
            },
            doDblClick($event) {
                // deselect text
                $event.stopImmediatePropagation();
                if (!this.grid.doubleclick)
                    return;
                window.getSelection().removeAllRanges();
                this.grid.doubleclick();
            },
            toggleDetails() {
                if (!this.detailsIcon)
                    return;
                const ui = this.row._uiprops_;
                ui.$details = !ui.$details;
            }
        }
    });
    const dataGridRowDetails = vue.defineComponent({
        name: 'data-grid-row-details',
        template: dataGridRowDetailsTemplate,
        props: {
            cols: Number,
            row: Object,
            mark: String
        },
        computed: {
            grid() {
                return this.$parent;
            },
            isMarkCell() {
                return this.grid.isMarkCell;
            },
            markClass() {
                return this.mark ? utils$d.simpleEval(this.row, this.mark) : '';
            },
            detailsMarker() {
                return this.grid.isRowDetailsCell;
            },
            totalCols() {
                return this.cols +
                    (this.isMarkCell ? 1 : 0) +
                    (this.detailsMarker ? 1 : 0);
            }
        },
        methods: {
            visible() {
                if (this.grid.isRowDetailsAlways)
                    return true;
                else if (this.grid.isRowDetailsCell)
                    return this.row._uiprops_.$details ? true : false;
                return this.row === this.grid.selected();
            }
        }
    });
    platform$3.vocabulary.component('data-grid', vue.defineComponent({
        inject: {
            page: { from: '$page', default: null }
        },
        props: {
            itemsSource: [Object, Array],
            border: Boolean,
            grid: String,
            fixedHeader: Boolean,
            hideHeader: Boolean,
            hover: {
                type: Boolean, default: undefined
            },
            striped: {
                type: Boolean,
                default: undefined
            },
            compact: Boolean,
            sort: Boolean,
            routeQuery: Object,
            mark: String,
            filterFields: String,
            markStyle: String,
            rowBold: String,
            doubleclick: Function,
            groupBy: [Array, Object, String],
            rowDetails: Boolean,
            rowDetailsActivate: String,
            rowDetailsVisible: [String /*path*/, Boolean],
            isItemActive: Function,
            hitItem: Function,
            emptyPanelCallback: Function,
            testId: String,
            autoSelect: String
        },
        template: dataGridTemplate,
        components: {
            'data-grid-row': dataGridRow,
            'data-grid-row-details': dataGridRowDetails
        },
        data() {
            return {
                columns: [],
                clientItems: null,
                clientGroups: null,
                localSort: {
                    dir: 'asc',
                    order: ''
                }
            };
        },
        computed: {
            $items() {
                return this.clientItems ? this.clientItems : this.itemsSource;
            },
            isMarkCell() {
                return this.markStyle === 'marker' || this.markStyle === 'both';
            },
            isRowDetailsCell() {
                return this.rowDetails && this.rowDetailsActivate === 'cell';
            },
            isRowDetailsAlways() {
                return this.rowDetails && this.rowDetailsActivate === 'always';
            },
            isMarkRow() {
                return this.markStyle === 'row' || this.markStyle === 'both';
            },
            isHeaderVisible() {
                return !this.hideHeader;
            },
            cssClass() {
                let cssClass = 'data-grid';
                if (this.grid)
                    cssClass += ' grid-' + this.grid.toLowerCase();
                if (this.striped != undefined)
                    cssClass += this.striped ? ' striped' : ' no-striped';
                if (this.hover != undefined)
                    cssClass += this.hover ? ' hover' : ' no-hover';
                if (this.compact)
                    cssClass += ' compact';
                return cssClass;
            },
            isGridSortable() {
                return !!this.sort;
            },
            isLocal() {
                return !this.$parent.sortDir;
            },
            isGrouping() {
                return this.groupBy;
            },
            groupColumns() {
                return this.columns.length + //1 +
                    (this.isMarkCell ? 1 : 0) +
                    (this.isRowDetailsCell ? 1 : 0);
            },
            $groupCount() {
                const groupBy = this.groupBy;
                if (utils$d.isString(groupBy))
                    return groupBy.split('-').length;
                else if (utils$d.isObjectExact(groupBy))
                    return 1;
                else
                    return groupBy.length;
            },
            $groups() {
                function* enumGroups(src, p0, lev, cnt) {
                    for (const grKey in src) {
                        if (grKey === 'items')
                            continue;
                        const srcElem = src[grKey];
                        const count = srcElem.items ? srcElem.items.length : 0;
                        if (cnt)
                            cnt.c += count;
                        const pElem = {
                            group: grKey,
                            p0: p0,
                            expanded: true,
                            level: lev,
                            items: srcElem.items || null,
                            count: count
                        };
                        yield pElem;
                        if (!src.items) {
                            const cnt = { c: 0 };
                            yield* enumGroups(srcElem, pElem, lev + 1, cnt);
                            pElem.count += cnt.c;
                        }
                    }
                }
                this.doSortLocally();
                // classic tree
                const startTime = performance.now();
                const grmap = {};
                let grBy = this.groupBy;
                if (utils$d.isString(grBy)) {
                    const rarr = [];
                    for (const vs of grBy.split('-'))
                        rarr.push({ prop: vs.trim(), count: true });
                    grBy = rarr;
                }
                else if (utils$d.isObjectExact(grBy))
                    grBy = [grBy];
                if (!this.$items)
                    return null;
                for (const itm of this.$items) {
                    let root = grmap;
                    for (const gr of grBy) {
                        let key = utils$d.eval(itm, gr.prop);
                        if (utils$d.isDate(key))
                            key = utils$d.format(key, 'Date');
                        if (!utils$d.isDefined(key))
                            key = '';
                        if (key === '')
                            key = locale$9.$Unknown || 'Unknown';
                        if (!(key in root))
                            root[key] = {};
                        root = root[key];
                    }
                    if (!root.items)
                        root.items = [];
                    root.items.push(itm);
                }
                // tree to plain array
                const grArray = [];
                for (const el of enumGroups(grmap, null, 1)) {
                    el.source = grBy[el.level - 1];
                    if (el.source.expanded === false)
                        el.expanded = false;
                    grArray.push(el);
                }
                this.clientGroups = grArray;
                log.time('datagrid grouping time:', startTime);
                return this.clientGroups;
            },
            $isEmpty() {
                const src = this.itemsSource;
                if (!src)
                    return false;
                const mi = src.$ModelInfo;
                if (!mi)
                    return src.length === 0;
                if ('HasRows' in mi) {
                    if (src.length)
                        return false;
                    return mi.HasRows === false;
                }
                else {
                    return src.length === 0;
                }
            }
        },
        watch: {
            localSort: {
                handler() {
                    this.handleSort();
                },
                deep: true
            },
            'itemsSource.length'() {
                this.handleSort();
            },
            '$isEmpty'(newval) {
                if (this.emptyPanelCallback)
                    this.emptyPanelCallback.call(this.page.$data, newval);
            }
        },
        methods: {
            selected() {
                let src = this.itemsSource;
                if (src.$origin)
                    src = src.$origin;
                return src.$selected;
            },
            $addColumn(column) {
                this.columns.push(column);
            },
            $removeColumn(column) {
                const ix = this.columns.indexOf(column);
                if (ix !== -1)
                    this.columns.splice(ix, 1);
            },
            rowDetailsShow(item) {
                if (!this.rowDetails)
                    return false;
                if (utils$d.isBoolean(this.rowDetailsVisible))
                    return this.rowDetailsVisible; // unset
                const vis = item[this.rowDetailsVisible];
                return !!vis;
            },
            columnClass(column) {
                let cls = '';
                if (column.fit || column.controlType === 'validator')
                    cls += 'fit';
                if (this.sort && column.isSortable && utils$d.isDefined(column.dir))
                    cls += ' sorted';
                if (column.backColor) {
                    cls += ` ${column.backColor}`;
                }
                return cls;
            },
            columnStyle(column) {
                return {
                    width: utils$d.isDefined(column.width) ? column.width : undefined,
                    minWidth: utils$d.isDefined(column.minWidth) ? column.minWidth : undefined
                };
            },
            doSort(order) {
                // TODO: // collectionView || locally
                if (this.isLocal) {
                    if (eqlower(this.localSort.order, order))
                        this.localSort.dir = eqlower(this.localSort.dir, 'asc') ? 'desc' : 'asc';
                    else {
                        this.localSort = { order: order, dir: 'asc' };
                    }
                }
                else {
                    this.$parent.doSort(order);
                }
            },
            sortDir(order) {
                if (this.isLocal)
                    return eqlower(this.localSort.order, order) ? this.localSort.dir : undefined;
                else
                    return this.$parent.sortDir(order);
            },
            doSortLocally() {
                if (!this.isLocal)
                    return;
                if (!this.localSort.order)
                    return;
                const startTime = performance.now();
                const rev = this.localSort.dir === 'desc';
                const sortProp = this.localSort.order;
                const arr = [].concat(this.itemsSource);
                arr.sort((a, b) => {
                    const av = a[sortProp];
                    const bv = b[sortProp];
                    if (av === bv)
                        return 0;
                    else if (av < bv)
                        return rev ? 1 : -1;
                    else
                        return rev ? -1 : 1;
                });
                log.time('datagrid sorting time:', startTime);
                this.clientItems = arr;
            },
            handleSort() {
                if (this.isGrouping)
                    this.clientGroups = null;
                else
                    this.doSortLocally();
            },
            toggleGroup(g) {
                g.expanded = !g.expanded;
            },
            isGroupCountVisible(g) {
                if (g && g.source && utils$d.isDefined(g.source.count))
                    return g.source.count;
                return true;
            },
            isGroupGroupVisible(g) {
                if (!g.group)
                    return false;
                if (!g.p0)
                    return true;
                let cg = g.p0;
                while (cg) {
                    if (!cg.expanded)
                        return false;
                    cg = cg.p0;
                }
                return true;
            },
            isGroupBodyVisible(g) {
                if (!g.expanded)
                    return false;
                let cg = g.p0;
                while (cg) {
                    if (!cg.expanded)
                        return false;
                    cg = cg.p0;
                }
                return true;
            },
            groupTitle(g) {
                if (g.source && g.source.title)
                    return g.source.title
                        .replace('{Value}', g.group)
                        .replace('{Count}', g.count);
                return g.group;
            },
            expandGroups(lev) {
                // lev 1-based
                for (const gr of this.$groups)
                    gr.expanded = gr.level < lev;
            },
            $checkAll(path) {
                if (!this.$items)
                    return;
                const s = this.$checkAllState(path);
                const val = s != 1;
                this.$items.forEach((itm) => itm[path] = val);
            },
            $checkAllState(path) {
                // 0 - unchecked; 1 - checked, 2-indeterminate
                if (!this.$items)
                    return 0;
                let checked = 0;
                let unchecked = 0;
                this.$items.forEach((itm) => {
                    if (itm[path])
                        checked += 1;
                    else
                        unchecked += 1;
                });
                if (checked === 0)
                    return 0;
                else if (unchecked === 0)
                    return 1;
                return 2;
            },
            __autoSelect() {
                if (!this.autoSelect || !this.$items || !this.$items.length)
                    return;
                if (this.$items.$selected)
                    return;
                switch (this.autoSelect) {
                    case 'first-item':
                        this.$items[0].$select();
                        break;
                    case 'last-item':
                        this.$items[this.$items.length - 1].$select();
                }
            },
            __invoke__test__(args) {
                args = args || {};
                if (args.target !== 'datagrid')
                    return;
                if (args.testId !== this.testId)
                    return;
                switch (args.action) {
                    case 'selectRow':
                        this.$items.forEach((e) => {
                            if (e.$id.toString() === args.id) {
                                e.$select();
                                args.result = 'success';
                            }
                        });
                        break;
                }
            }
        },
        updated() {
            const src = this.itemsSource;
            if (!src)
                return;
            const ix = src.$selectedIndex;
            const rows = this.$refs.row;
            const row = ix !== -1 && rows ? rows.find(r => r.index === ix) : null;
            if (row) {
                const tr = row.$refs.tr;
                tr.scrollIntoViewCheck();
            }
            this.__autoSelect();
        },
        mounted() {
            if (this.testId)
                eventBus$a.$on('invokeTest', this.__invoke__test__);
        },
        beforeUnmount() {
            if (this.testId)
                eventBus$a.$off('invokeTest', this.__invoke__test__);
        }
    }));

    // v-auto-size (Net48 directives/autosize.js, 'autoSize'), unchanged in behaviour: a textarea grows with its text, from
    // its first height; 'autosize', dispatched on it, measures again after the update (a2-textarea, a value set by code).
    // Vue 2's bind is created, inserted is mounted.
    platform$3.vocabulary.directive('autoSize', {
        created(el, binding) {
            if (!binding.value)
                return;
            el.style.overflowY = '';
            el._ops = {
                initHeight: -1,
                extraSpace: 0
            };
            el._autosize = function () {
                if (!el.offsetHeight)
                    return;
                const ops = el._ops;
                if (ops.initHeight === -1) {
                    ops.initHeight = el.offsetHeight;
                }
                el.style.height = ops.initHeight + 'px';
                const needHeight = el.scrollHeight + ops.extraSpace;
                if (needHeight > ops.initHeight)
                    el.style.height = needHeight + 'px';
            };
            function onInput() {
                el._autosize();
            }
            function onAutoSize() {
                vue.nextTick(() => el._autosize());
            }
            el.addEventListener('input', onInput);
            // Perhaps the value was set programmatically.
            el.addEventListener('autosize', onAutoSize);
        },
        mounted(el, binding) {
            if (!binding.value)
                return;
            const style = window.getComputedStyle(el);
            const es = parseFloat(style.borderTopWidth) + parseFloat(style.borderBottomWidth);
            el._ops.extraSpace = es;
            setTimeout(() => el._autosize(), 1);
        }
    });

    // a2-taskpad, a2-panel (Net48 components/taskpad.js, panel.js), unchanged in behaviour: the page's side pane, a
    // collapsible panel.
    platform$3.vocabulary.component('a2-taskpad', vue.defineComponent({
        template: `<div :class="cssClass" :style="{width:width}">
	<a class="ico taskpad-collapse-handle" @click.stop="toggle"></a>
	<div v-if="expanded" class="taskpad-body">
		<slot>
		</slot>
	</div>
	<div v-else class="taskpad-title" @click.prevent="toggle">
		<span class="taskpad-label" v-text="tasksText"></span>
	</div>
</div>
`,
        props: {
            title: String,
            initialCollapsed: Boolean,
            position: String,
            initialWidth: { type: String, default: '20rem' }
        },
        data() {
            return {
                expanded: true
            };
        },
        computed: {
            width() {
                return this.expanded ? this.initialWidth : undefined;
            },
            cssClass() {
                let cls = 'taskpad';
                cls += ' position-' + this.position;
                if (this.expanded)
                    cls += ' expanded';
                else
                    cls += ' collapsed';
                return cls;
            },
            tasksText() {
                return this.title || window.$$locale.$Tasks;
            }
        },
        methods: {
            setExpanded(exp) {
                this.expanded = exp;
            },
            toggle() {
                this.setExpanded(!this.expanded);
            }
        },
        mounted() {
            if (this.initialCollapsed)
                this.setExpanded(false);
        }
    }));
    platform$3.vocabulary.component('a2-panel', vue.defineComponent({
        template: `<div :class="cssClass" :test-id="testId">
	<div class="panel-header" @click.prevent="toggle" v-if="!noHeader">
		<slot name='header'></slot>
		<span v-if="collapsible" class="ico panel-collapse-handle"></span>
	</div>
	<slot v-if="expanded"></slot>
</div>
`,
        props: {
            initialCollapsed: Boolean,
            collapsible: Boolean,
            panelStyle: String,
            noHeader: Boolean,
            testId: String
        },
        data() {
            return {
                collapsed: this.initialCollapsed
            };
        },
        computed: {
            cssClass() {
                let cls = 'panel';
                if (this.collapsed)
                    cls += ' collapsed';
                else
                    cls += ' expanded';
                if (this.panelStyle) {
                    const ps = this.panelStyle.toLowerCase();
                    switch (ps) {
                        case 'red':
                        case 'danger':
                        case 'error':
                            cls += ' panel-red';
                            break;
                        case 'info':
                        case 'cyan':
                            cls += ' panel-cyan';
                            break;
                        case 'green':
                        case 'success':
                            cls += ' panel-green';
                            break;
                        case 'warning':
                        case 'yellow':
                            cls += ' panel-yellow';
                            break;
                        default:
                            cls += ' panel-' + ps;
                            break;
                    }
                }
                return cls;
            },
            expanded() {
                return !this.collapsed;
            }
        },
        methods: {
            toggle() {
                if (!this.collapsible)
                    return;
                this.collapsed = !this.collapsed;
            }
        }
    }));

    // a2-calendar (Net48 components/calendar.js), unchanged in behaviour: a month of days, or a year of months; the owner
    // (date picker, period picker) moves it and takes the day (setMonth, setDay).
    // Vue 3's compiler refuses children under v-text: the weekday header has none.
    const utils$c = require$1('std:utils');
    const locale$8 = window.$$locale;
    platform$3.vocabulary.component('a2-calendar', vue.defineComponent({
        template: `
<div @click.stop.prevent="dummy">
	<table class="calendar-pane">
		<thead><tr>
				<th class="h-btn"><a v-if="hasPrev" @click.stop.prevent="prevMonth"><i class="ico ico-triangle-left"></i></a></th>
				<th :colspan="isDayView ? 5 : 7" class="month-title"><span v-text="title"></span></th>
				<th class="h-btn"><a v-if="hasNext" @click.stop.prevent='nextMonth'><i class="ico ico-triangle-right"></i></a></th>
			</tr>
			<tr class="weekdays" v-if="isDayView"><th v-for="d in 7" v-text="wdTitle(d)"></th></tr>
			<tr class="weekdays" v-else><th colspan="9"></th></tr>
		</thead>
		<tbody v-if="isDayView">
			<tr v-for="row in days">
				<td v-for="day in row" :class="dayClass(day)"><a @click.stop.prevent="selectDay(day)"
					@mouseover="mouseOver(day)"
					v-text="day.getDate()" :title="dayTitle(day)"/></td>
			</tr>
		</tbody>
		<tbody v-else>
			<tr v-for="qr in monthes">
				<td v-for="m in qr" class="mcell" :class="monthClass(m.date)" colspan="3"><a @click.stop.prevent="selectDay(m.date)" v-text="m.name"/></td>
			</tr>
		</tbody>
		<tfoot v-if="showFooter" ><tr><td colspan="7" class="calendar-footer">
			<a class="today" @click.stop.prevent='today' v-text='todayText'></a></td></tr></tfoot>
	</table>
</div>
`,
        props: {
            showToday: { type: Boolean, default: true },
            pos: String,
            model: Date,
            setMonth: Function,
            setDay: Function,
            getDayClass: Function,
            hover: Function,
            view: String,
            currentModel: Date
        },
        computed: {
            isDayView() {
                return (this.view || 'day') === 'day';
            },
            showFooter() {
                return this.showToday && this.isDayView;
            },
            days() {
                const dt = new Date(this.model);
                dt.setDate(1); // 1-st day of month
                let w = dt.getDay() - 1; // weekday
                if (w === -1)
                    w = 6;
                dt.setDate(-w + 1);
                const arr = [];
                for (let r = 0; r < 6; r++) {
                    const row = [];
                    for (let c = 0; c < 7; c++) {
                        row.push(new Date(dt));
                        dt.setDate(dt.getDate() + 1);
                    }
                    arr.push(row);
                }
                return arr;
            },
            monthes() {
                const ma = [];
                for (let q = 0; q < 4; q++) {
                    const ia = [];
                    for (let m = 0; m < 3; m++) {
                        const mno = q * 3 + m;
                        const dt = utils$c.date.create(this.model.getFullYear(), mno + 1, 1);
                        const mname = utils$c.text.capitalize(dt.toLocaleDateString(locale$8.$Locale, { month: 'long' }));
                        ia.push({ date: dt, month: mno + 1, name: mname });
                    }
                    ma.push(ia);
                }
                return ma;
            },
            hasPrev() {
                return this.pos !== 'right';
            },
            hasNext() {
                return this.pos !== 'left';
            },
            title() {
                const mn = this.model.toLocaleString(locale$8.$Locale, { month: 'long', year: 'numeric' });
                return utils$c.text.capitalize(mn);
            },
            todayText() {
                return locale$8.$Today;
            },
            todayDate() {
                return utils$c.date.today();
            }
        },
        methods: {
            dummy() { },
            nextMonth() {
                let dt = new Date(this.model);
                if (this.isDayView)
                    dt = utils$c.date.add(dt, 1, 'month');
                else
                    dt.setFullYear(dt.getFullYear() + 1);
                this.setMonth(dt, this.pos);
            },
            prevMonth() {
                let dt = new Date(this.model);
                if (this.isDayView)
                    dt = utils$c.date.add(dt, -1, 'month');
                else
                    dt.setFullYear(dt.getFullYear() - 1);
                this.setMonth(dt, this.pos);
            },
            wdTitle(d) {
                const dt = this.days[0][d - 1];
                return dt.toLocaleString(locale$8.$Locale, { weekday: 'short' });
            },
            today() {
                this.setDay(this.todayDate);
            },
            selectDay(d) {
                this.setDay(d, this.pos);
            },
            dayClass(day) {
                let cls = '';
                if (day.getMonth() !== this.model.getMonth())
                    cls += ' other';
                if (this.getDayClass) {
                    cls += this.getDayClass(day);
                    return cls;
                }
                const tls = utils$c.date;
                if (tls.equal(day, this.todayDate))
                    cls += ' today';
                if (tls.equal(day, this.model))
                    cls += ' active';
                return cls;
            },
            monthClass(day) {
                let cls = '';
                const cm = this.currentModel || this.model;
                if (day.getFullYear() === cm.getFullYear() && day.getMonth() === cm.getMonth())
                    cls += ' active';
                return cls;
            },
            dayTitle(day) {
                return day.toLocaleString(locale$8.$Locale, { year: 'numeric', month: 'long', day: 'numeric', weekday: 'long' });
            },
            mouseOver(day) {
                if (this.hover)
                    this.hover(day);
            }
        }
    }));

    // a2-period-picker (Net48 components/periodpicker.js), unchanged in behaviour: over control; the period of item[prop],
    // chosen from the predefined menu, two calendars or typed dates; applied, it sets the model dirty and emits
    // <path>.<prop>.change. A popup: a click outside closes it.
    // Vue 3's compiler refuses attributes with no space between them (one calendar's style and :show-today): spaced.
    const popup$6 = require$1('std:popup');
    const utils$b = require$1('std:utils');
    const eventBus$9 = require$1('std:eventBus');
    const uPeriod = require$1('std:period');
    const du$1 = utils$b.date;
    const DEFAULT_DEBOUNCE = 150;
    platform$3.vocabulary.component('a2-period-picker', vue.defineComponent({
        extends: control,
        template: `
<div class="control-group period-picker" @click.stop.prevent="toggle($event)" :class="{open: isOpen, disabled: disabled}">
	<label v-if="hasLabel"><span v-text="label"/><slot name="hint"/></label>
	<div class="input-group">
		<span class="period-text" v-text="text" :class="inputClass" :tabindex="tabIndex"/>
		<span class="caret"/>
		<div class="calendar period-pane" v-if="isOpen" @click.stop.prevent="dummy">
			<ul class="period-menu" style="grid-area: 1 / 1 / span 2 / auto">
				<li v-for='(mi, ix) in menu' :key="ix" :class="{active: isSelectedMenu(mi.key)}"><a v-text='mi.name' @click.stop.prevent="selectMenu(mi.key)"></a></li>
			</ul>
			<a2-calendar style="grid-area: 1 / 2" :show-today="false" pos="left" :model="prevModelDate"
				:set-month="setMonth" :set-day="setDay" :get-day-class="dayClass" :hover="mouseHover"/>
			<a2-calendar style="grid-area: 1 / 3; margin-left:6px" :show-today="false" pos="right" :model="modelDate"
				:set-month="setMonth" :set-day="setDay" :get-day-class="dayClass" :hover="mouseHover"/>
			<div class="period-footer" style="grid-area: 2 / 2 / auto / span 2">
				<div v-if="customMode" class="custom-period">
					<div><input type=text v-model.lazy.trim="customStart" v-focus></div>
					<span> - </span>
					<div><input type=text v-model.lazy.trim="customEnd" v-focus></div>
				</div>
				<a v-else class="current-period" v-text="currentText" :class="{processing: selection}" href="" @click.stop.prevent=startCustom></a>
				<span class="aligner"></span>
				<button class="btn btn-primary" @click.stop.prevent="apply" v-text="locale.$Apply" :disabled="applyDisabled"/>
				<button class="btn btn-default" @click.stop.prevent="close" v-text="locale.$Cancel" />
			</div>
		</div>
	</div>
	<span class="descr" v-if="hasDescr" v-text="description"></span>
</div>
`,
        props: {
            item: Object,
            prop: String,
            showAll: {
                type: Boolean,
                default: true
            },
            display: String,
            callback: Function
        },
        data() {
            return {
                isOpen: false,
                selection: '',
                modelDate: du$1.today(),
                currentPeriod: uPeriod.zero(),
                selectEnd: du$1.zero(),
                timerId: undefined,
                customMode: false
            };
        },
        computed: {
            locale() {
                return window.$$locale;
            },
            text() {
                if (this.display === 'name')
                    return this.period.text();
                else if (this.display === 'namedate') {
                    if (this.period.isAllData())
                        return this.period.text(true);
                    return `${this.period.text(true)} [${this.period.format('Date')}]`;
                }
                return this.period.format('Date');
            },
            period() {
                if (!this.item)
                    return this.currentPeriod;
                const period = this.item[this.prop];
                if (!uPeriod.isPeriod(period))
                    console.error('PeriodPicker. Value is not a Period');
                return period;
            },
            prevModelDate() {
                return du$1.add(this.modelDate, -1, 'month');
            },
            currentText() {
                return this.currentPeriod.format('Date');
            },
            applyDisabled() {
                return this.selection === 'start';
            },
            menu() {
                return uPeriod.predefined(this.showAll);
            },
            customStart: {
                get() {
                    const dt = this.currentPeriod.From;
                    if (du$1.equal(dt, du$1.minDate) || du$1.equal(dt, du$1.maxDate))
                        return '';
                    return du$1.format(dt);
                },
                set(val) {
                    const dat = this.parseDate(val);
                    this.currentPeriod.setFrom(dat);
                    this.modelDate = this.currentPeriod.To;
                }
            },
            customEnd: {
                get() {
                    const dt = this.currentPeriod.To;
                    if (du$1.equal(dt, du$1.minDate) || du$1.equal(dt, du$1.maxDate))
                        return '';
                    return du$1.format(dt);
                },
                set(val) {
                    const dat = this.parseDate(val);
                    this.currentPeriod.setTo(dat);
                    this.modelDate = this.currentPeriod.To;
                }
            }
        },
        methods: {
            dummy() {
            },
            setMonth(d, pos) {
                this.customMode = false;
                if (pos === 'left')
                    this.modelDate = du$1.add(d, 1, 'month'); // prev month
                else
                    this.modelDate = d;
            },
            setDay(d) {
                this.customMode = false;
                if (!this.selection) {
                    this.selection = 'start';
                    this.currentPeriod.From = d;
                    this.currentPeriod.To = du$1.zero();
                    this.selectEnd = du$1.zero();
                }
                else if (this.selection === 'start') {
                    this.currentPeriod.To = d;
                    this.currentPeriod.normalize();
                    this.selection = '';
                }
            },
            dayClass(day) {
                let cls = '';
                let px = this.currentPeriod;
                if (this.selection)
                    px = uPeriod.create('custom', this.currentPeriod.From, this.selectEnd);
                if (px.in(day))
                    cls += ' active';
                if (px.From.getTime() === day.getTime())
                    cls += ' period-start';
                if (px.To.getTime() === day.getTime())
                    cls += ' period-end';
                return cls;
            },
            close() {
                this.isOpen = false;
                this.customMode = false;
            },
            apply() {
                // apply period here
                if (!this.period.equal(this.currentPeriod)) {
                    this.period.assign(this.currentPeriod);
                    this.fireEvent();
                }
                this.close();
            },
            fireEvent() {
                const item = this.item;
                if (this.callback)
                    this.callback(this.period);
                const root = item.$root;
                if (!root)
                    return;
                const path = item._path_ || 'global';
                const eventName = `${path}.${this.prop}.change`;
                if (root && root.$setDirty)
                    root.$setDirty(true, path, this.prop);
                root.$emit(eventName, item, this.period, null);
            },
            toggle() {
                if (!this.isOpen) {
                    // close other popups
                    eventBus$9.$emit('closeAllPopups');
                    if (this.disabled)
                        return;
                    this.modelDate = this.period.To; // TODO: calc start month
                    if (this.modelDate.isZero() || this.modelDate.getTime() === du$1.maxDate.getTime())
                        this.modelDate = du$1.today();
                    this.currentPeriod.assign(this.period);
                    this.selection = '';
                    this.selectEnd = du$1.zero();
                    this.customMode = false;
                }
                this.isOpen = !this.isOpen;
            },
            __clickOutside() {
                this.close();
            },
            isSelectedMenu(key) {
                const p = uPeriod.create(key);
                return p.equal(this.currentPeriod);
            },
            selectMenu(key) {
                const p = uPeriod.create(key);
                this.currentPeriod.assign(p);
                this.apply();
            },
            mouseHover(day) {
                if (!this.selection)
                    return;
                if (this.selectEnd.getTime() === day.getTime())
                    return;
                clearTimeout(this.timerId);
                this.timerId = setTimeout(() => {
                    this.selectEnd = day;
                }, DEFAULT_DEBOUNCE);
            },
            startCustom() {
                this.customMode = true;
            },
            parseDate(val) {
                let dat = du$1.parse(val);
                if (du$1.isZero(dat))
                    dat = du$1.today();
                else if (dat.getTime() < du$1.minDate.getTime())
                    dat = du$1.minDate;
                else if (dat.getTime() > du$1.maxDate.getTime())
                    dat = du$1.maxDate;
                return dat;
            }
        },
        mounted() {
            popup$6.registerPopup(this.$el);
            this.$el._close = this.__clickOutside;
        },
        beforeUnmount() {
            popup$6.unregisterPopup(this.$el);
        }
    }));

    // a2-selector (Net48 components/selector.js), unchanged in behaviour: over control; a reference typed or picked from a
    // list fetched as typed (fetch, fetchCommand through the page's $invoke, or itemsSource); a popup.
    // Vue 3: $root was the page (base.js): here the page's controller (inject $page): fetchData's plain-object $vm.
    const popup$5 = require$1('std:popup');
    const utils$a = require$1('std:utils');
    const platform$2 = require$1('std:platform');
    const locale$7 = window.$$locale;
    const eventBus$8 = require$1('std:eventBus');
    const DEFAULT_DELAY = 300;
    platform$3.vocabulary.component('a2-selector', vue.defineComponent({
        extends: control,
        template: `
<div :class="cssClass2()"  :test-id="testId">
	<label v-if="hasLabel"><span v-text="label"/><slot name="hint"/><slot name="link"></slot></label>
	<div class="input-group">
		<div v-if="isCombo" class="selector-combo" :class="colorClass" @click.stop.prevent="open"><span tabindex="-1" class="select-text" v-text="valueText" @keydown="keyDown" ref="xcombo"/></div>
		<input v-focus v-model="query" :class="[inputClass, colorClass]" :placeholder="placeholder" v-else
			@input="debouncedUpdate" @blur.stop="blur" @keydown="keyDown" @keyup="keyUp" ref="input"
			:readonly="disabled" @click="clickInput($event)" :tabindex="tabIndex"/>
		<slot></slot>
		<a class="selector-open" href="" @click.stop.prevent="open" v-if="caret"><span class="caret"></span></a>
		<a class="selector-clear" href="" @click.stop.prevent="clear" v-if="clearVisible" tabindex="-1">&#x2715;</a>
		<validator :invalid="invalid" :errors="errors" :options="validatorOptions"></validator>
		<div class="selector-pane" v-if="isOpen" ref="pane" :class="paneClass">
			<div class="selector-body" :style="bodyStyle">
				<slot name="pane" :items="items" :is-item-active="isItemActive2" :item-name="itemName" :hit="hit" :max-chars="maxChars" :line-clamp="lineClamp" :slotStyle="slotStyle">
					<ul class="selector-ul">
						<li @mousedown.prevent="hit(itm)" :class="{'active': isItemActive(itmIndex)}"
							v-for="(itm, itmIndex) in items" :key="itmIndex">
							<span :style="itemStyle(itm)" :class="itemClass(itm, itmIndex)" :title="itemTitle(itm)" v-text="itemName(itm)"></span>
						</li>
					</ul>
				</slot>
			</div>
			<a v-if='canNew' class="create-elem a2-hyperlink a2-inline" @mousedown.stop.prevent="doNew()"><i class="ico ico-plus"/> <span v-text="newText"></span></a>
		</div>
		<div class="selector-pane" v-if="isOpenNew" @click.stop.prevent="dummy">
			<slot name="new-pane"></slot>
		</div>
	</div>
	<slot name="popover"></slot>
	<span class="descr" v-if="hasDescr" v-text="description"></span>
</div>
`,
        inject: {
            page: { from: '$page', default: null }
        },
        props: {
            item: Object,
            prop: String,
            itemsSource: Array,
            textItem: Object,
            textProp: String,
            colorProp: String,
            display: String,
            itemToValidate: Object,
            propToValidate: String,
            placeholder: String,
            delay: Number,
            minChars: Number,
            fetch: Function,
            hitfunc: Function,
            listWidth: String,
            listHeight: String,
            createNew: Function,
            placement: String,
            caret: Boolean,
            hasClear: Boolean,
            useAll: Boolean,
            mode: String,
            fetchCommand: String,
            fetchCommandData: Object,
            maxChars: Number,
            lineClamp: Number,
            highlight: Boolean
        },
        data() {
            return {
                isOpen: false,
                isOpenNew: false,
                loading: false,
                items: [],
                query: '',
                filter: '',
                current: -1
            };
        },
        computed: {
            valueText() {
                const item = this.item;
                if (!item)
                    return '';
                if (this.hasText) {
                    const el = item[this.prop];
                    if (el.$isEmpty)
                        return this.textItem[this.textProp];
                }
                let el = item[this.prop];
                if (utils$a.isNumber(el) && this.itemsSource) {
                    el = this.itemsSource.find(x => x.$id === el);
                    if (!el)
                        return '';
                }
                return utils$a.simpleEval(el, this.display);
            },
            hasValue() {
                if (!this.highlight)
                    return false;
                if (!this.item)
                    return false;
                const el = this.item[this.prop];
                if (utils$a.isObjectExact(el) && el.Id && el.Id != -1)
                    return true;
                return undefined;
            },
            canNew() {
                return !!this.createNew;
            },
            clearVisible() {
                if (!this.hasClear)
                    return false;
                const to = this.item[this.prop];
                if (!to)
                    return false;
                if (this.useAll && to.Id === -1)
                    return false;
                if (utils$a.isDefined(to.$isEmpty))
                    return !to.$isEmpty;
                return !utils$a.isPlainObjectEmpty(to);
            },
            hasText() { return !!this.textProp; },
            newText() {
                return `${locale$7.$CreateLC} "${this.query}"`;
            },
            pane() {
                return {
                    items: this.items,
                    isItemActive: this.isItemActive2,
                    itemName: this.itemName,
                    hit: this.hit
                };
            },
            paneClass() {
                if (this.placement)
                    return 'panel-' + this.placement;
                return undefined;
            },
            bodyStyle() {
                const s = {};
                if (this.listWidth) {
                    s.minWidth = this.listWidth;
                }
                if (this.listHeight)
                    s.maxHeight = this.listHeight;
                return s;
            },
            slotStyle() {
                const r = {};
                if (this.listWidth) {
                    r.width = this.listWidth;
                    r.minWidth = this.listWidth;
                }
                if (this.listHeight)
                    r.maxHeight = this.maxHeight;
                return r;
            },
            colorClass() {
                return this.item ? this.colorOf(this.item[this.prop]) : undefined;
            },
            debouncedUpdate() {
                const delay = this.delay || DEFAULT_DELAY;
                return utils$a.debounce(() => {
                    this.current = -1;
                    this.filter = this.query;
                    this.update();
                }, delay);
            },
            isCombo() {
                return this.mode === 'combo-box' || this.mode === 'hyperlink';
            }
        },
        watch: {
            valueText() {
                if (this.hasText) {
                    const el = this.item[this.prop];
                    if (!el.$isEmpty)
                        this.textItem[this.textProp] = ''; // clear text
                }
                this.query = this.valueText;
            }
        },
        methods: {
            dummy() {
            },
            __clickOutside() {
                this.isOpen = false;
                this.isOpenNew = false;
            },
            colorOf(el) {
                if (!this.colorProp)
                    return undefined;
                const clr = utils$a.simpleEval(el, this.colorProp);
                return clr ? `color color-${clr}` : undefined;
            },
            cssClass2() {
                let cx = this.cssClass();
                if (this.isOpen || this.isOpenNew)
                    cx += ' open';
                if (this.mode === 'hyperlink')
                    cx += ' selector-hyperlink';
                else if (this.mode === 'combo-box')
                    cx += ' selector-combobox';
                if (this.hasValue)
                    cx += ' has-value';
                return cx;
            },
            isItemActive(ix) {
                return ix === this.current;
            },
            isItemActive2(ix, row) {
                if (!row)
                    return this.isItemActive(ix);
                if (this.current === -1)
                    return false;
                return this.items[this.current] === row;
            },
            itemName(itm) {
                const v = utils$a.simpleEval(itm, this.display);
                if (this.maxChars)
                    return utils$a.text.maxChars(v, this.maxChars);
                return v;
            },
            blur() {
                const text = this.query;
                if (this.hasText && text !== this.valueText) {
                    const to = this.item[this.prop];
                    if (to && to.$empty)
                        to.$empty();
                    this.textItem[this.textProp] = text;
                }
                this.cancel();
            },
            open() {
                if (!this.isOpen) {
                    eventBus$8.$emit('closeAllPopups');
                    if (this.current != -1)
                        this.$nextTick(() => this.scrollIntoView());
                    this.doFetch(this.valueText, true);
                    if (this.isCombo) {
                        const combo = this.$refs['xcombo'];
                        if (combo)
                            combo.focus();
                    }
                    else {
                        const input = this.$refs['input'];
                        if (input)
                            input.focus();
                    }
                }
                this.isOpen = !this.isOpen;
            },
            cancel() {
                this.query = this.valueText;
                this.isOpen = false;
            },
            keyUp(event) {
                if (this.isOpen && event.which === 27) {
                    event.preventDefault();
                    event.stopPropagation();
                    this.cancel();
                }
                else if (event.which === 13) {
                    if (this.hasText)
                        this.blur();
                }
            },
            clickInput(event) {
                if (this.caret && !this.isOpen) {
                    event.stopPropagation();
                    event.preventDefault();
                    this.open();
                }
            },
            keyDown(event) {
                if (!this.isOpen) {
                    if (event.which === 115)
                        this.open();
                    return;
                }
                event.stopPropagation();
                switch (event.which) {
                    case 27: // esc
                        event.preventDefault();
                        break;
                    case 13: // enter
                        if (this.current === -1)
                            return;
                        this.hit(this.items[this.current]);
                        break;
                    case 40: // down
                        event.preventDefault();
                        this.current += 1;
                        if (this.current >= this.items.length)
                            this.current = 0;
                        this.query = this.itemName(this.items[this.current]);
                        this.scrollIntoView();
                        break;
                    case 38: // up
                        event.preventDefault();
                        this.current -= 1;
                        if (this.current < 0)
                            this.current = this.items.length - 1;
                        this.query = this.itemName(this.items[this.current]);
                        this.scrollIntoView();
                        break;
                    case 115: // F4
                        this.cancel();
                        break;
                    default:
                        return;
                }
            },
            itemTitle(itm) {
                if (this.lineClamp > 0)
                    return this.itemName(itm);
                return '';
            },
            itemStyle() {
                if (this.lineClamp > 0)
                    return { '-webkit-line-clamp': this.lineClamp };
                return undefined;
            },
            itemClass(itm) {
                let cls = '';
                if (this.lineClamp > 0)
                    cls += ' line-clamp';
                const clr = this.colorOf(itm);
                if (clr)
                    cls += ' ' + clr;
                return cls;
            },
            hit(itm) {
                const item = this.item;
                const obj = item[this.prop];
                this.query = this.valueText;
                this.isOpen = false;
                this.isOpenNew = false;
                this.current = this.items.indexOf(itm);
                this.$nextTick(() => {
                    if (this.hitfunc) {
                        this.hitfunc.call(item.$root, itm, item, this.prop);
                        return;
                    }
                    if (obj && obj.$merge)
                        obj.$merge(itm, true /*fire*/);
                    else if (utils$a.isNumber(obj))
                        item[this.prop] = itm.$id;
                    else
                        platform$2.set(item, this.prop, itm);
                });
            },
            clear() {
                this.query = '';
                this.isOpen = false;
                this.isOpenNew = false;
                const obj = this.item[this.prop];
                if (!obj)
                    return;
                if (this.useAll) {
                    obj.Id = -1;
                    obj.Name = '';
                    return;
                }
                if (obj.$empty)
                    obj.$empty();
                else if (utils$a.isObjectExact(obj))
                    utils$a.clearObject(obj);
            },
            scrollIntoView() {
                this.$nextTick(() => {
                    const pane = this.$refs['pane'];
                    if (!pane)
                        return;
                    const elem = pane.querySelector('.active');
                    if (!elem)
                        return;
                    elem.scrollIntoView(false);
                });
            },
            update() {
                const text = this.query || '';
                const chars = +(this.minChars || 0);
                if (chars && text.length < chars)
                    return;
                this.isOpen = true;
                this.isOpenNew = false;
                if (text === '') {
                    this.clear();
                    return;
                }
                this.doFetch(text, false);
            },
            doFetch(text, all) {
                if (this.itemsSource) {
                    if (!this.items.length)
                        this.items = this.itemsSource;
                    let fi = -1;
                    const el = this.item[this.prop];
                    if (utils$a.isNumber(el))
                        fi = this.items.findIndex((x) => x.Id === el);
                    else
                        fi = this.items.indexOf(el);
                    if (fi !== -1) {
                        this.current = fi;
                        setTimeout(() => this.scrollIntoView(), 10);
                    }
                    return;
                }
                this.loading = true;
                const fData = this.fetchData(text, all);
                if (fData.then) {
                    // promise
                    fData.then((result) => {
                        this.loading = false;
                        // first property from result
                        const prop = Object.keys(result)[0];
                        this.items = result[prop];
                        if (this.items.length)
                            this.current = 0;
                    }).catch(() => {
                        this.items = [];
                    });
                }
                else {
                    if (utils$a.isArray(fData)) {
                        if (this.items != fData) {
                            if (this.items.length != fData.length)
                                this.current = -1; // reset current element
                            this.items = fData;
                        }
                    }
                }
            },
            fetchData(text, all) {
                all = all || false;
                const item = this.item;
                const elem = item[this.prop];
                if (elem && !('$vm' in elem)) {
                    // plain object hack
                    Object.defineProperty(elem, '$vm', { value: this.page, writable: false, enumerable: false });
                }
                if (this.fetch) {
                    return this.fetch.call(item.$root, elem, text, all);
                }
                else if (this.fetchCommand) {
                    if (!text)
                        return [];
                    const fc = this.fetchCommand.split('/');
                    const action = fc.pop();
                    const invokeArg = Object.assign({}, { Text: text }, this.fetchCommandData);
                    return elem.$vm.$invoke(action, invokeArg, fc.join('/'));
                }
                console.error('Selector. Fetch or Delegate not defined');
            },
            doNew() {
                this.isOpen = false;
                if (this.createNew) {
                    const elem = this.item[this.prop];
                    const arg = {
                        elem: elem,
                        text: this.query
                    };
                    this.createNew(arg);
                }
            }
        },
        mounted() {
            popup$5.registerPopup(this.$el);
            this.query = this.valueText;
            this.$el._close = this.__clickOutside;
        },
        beforeUnmount() {
            popup$5.unregisterPopup(this.$el);
        }
    }));

    // a2-date-picker (Net48 components/datepicker.js), unchanged in behaviour: over control; a date typed, or picked on a
    // calendar (a month, for view 'month'); the time of the model's date is kept. A popup.
    const popup$4 = require$1('std:popup');
    const utils$9 = require$1('std:utils');
    const eventBus$7 = require$1('std:eventBus');
    const locale$6 = window.$$locale;
    const dateLocale$2 = locale$6.$DateLocale || locale$6.$Locale;
    const monthLocale$1 = locale$6.$Locale; // for text
    platform$3.vocabulary.component('a2-date-picker', vue.defineComponent({
        extends: control,
        template: `
<div :class="cssClass2()" class="date-picker" :test-id="testId">
	<label v-if="hasLabel"><span v-text="label"/><slot name="hint"/><slot name="link"></slot></label>
	<div class="input-group"  @click="clickInput($event)">
		<input v-focus v-model.lazy="model" v-if="!isMonth" :class="inputClass" :readonly="inputDisabled"/>
		<div class="month-wrapper" v-if=isMonth v-text=model></div>
		<a href @click.stop.prevent="toggle($event)" tabindex="-1"><i class="ico ico-calendar"></i></a>
		<a href v-if="clearVisible" @click.stop.prevent="clear($event)" tabindex="-1">✕</a>
		<validator :invalid="invalid" :errors="errors" :options="validatorOptions"></validator>
		<div class="calendar" v-if="isOpen">
			<a2-calendar :model="viewDate" :view="view" :current-model="modelDate"
				:set-month="setMonth" :set-day="selectDay" :get-day-class="dayClass"/>
		</div>
	</div>
	<span class="descr" v-if="hasDescr" v-text="description"></span>
</div>
`,
        props: {
            item: Object,
            prop: String,
            itemToValidate: Object,
            propToValidate: String,
            // override control.align (default value)
            align: { type: String, default: 'center' },
            view: String,
            yearCutOff: String,
            hasClear: Boolean
        },
        data() {
            return {
                isOpen: false,
                viewDate: null
            };
        },
        watch: {
            modelDate() {
                this.viewDate = this.modelDate;
            }
        },
        methods: {
            toggle() {
                if (this.disabled)
                    return;
                if (!this.isOpen) {
                    // close other popups
                    eventBus$7.$emit('closeAllPopups');
                    if (utils$9.date.isZero(this.modelDate))
                        this.viewDate = utils$9.date.today();
                }
                this.isOpen = !this.isOpen;
            },
            clear() {
                this.isOpen = false;
                this.updateModel(utils$9.date.zero());
            },
            updateModel(date) {
                this.item[this.prop] = date;
                this.viewDate = date;
            },
            fitDate(dt) {
                const du = utils$9.date;
                if (du.isZero(dt))
                    return dt;
                if (dt < du.minDate)
                    dt = du.minDate;
                else if (dt > du.maxDate)
                    dt = du.maxDate;
                return dt;
            },
            clickInput(ev) {
                if (this.view === 'month') {
                    this.toggle();
                    ev.stopPropagation();
                    ev.preventDefault();
                }
            },
            setMonth(dt) {
                this.viewDate = dt;
            },
            selectDay(day) {
                const dt = new Date(day.getFullYear(), day.getMonth(), day.getDate(), 0, 0, 0, 0);
                this.setDate(dt);
                this.isOpen = false;
            },
            setDate(d) {
                // save time
                const md = this.modelDate || d;
                let nd = new Date(d.getFullYear(), d.getMonth(), d.getDate(), md.getHours(), md.getMinutes(), 0, 0);
                nd = this.fitDate(nd);
                this.updateModel(nd);
            },
            dayClass(day) {
                let cls = '';
                const tls = utils$9.date;
                if (tls.equal(day, tls.today()))
                    cls += ' today';
                if (tls.equal(day, this.modelDate))
                    cls += ' active';
                if (day.getMonth() !== this.viewDate.getMonth())
                    cls += ' other';
                return cls;
            },
            cssClass2() {
                let cx = this.cssClass();
                if (this.isOpen)
                    cx += ' open';
                return cx;
            },
            __clickOutside() {
                this.isOpen = false;
            }
        },
        computed: {
            modelDate() {
                return this.item[this.prop];
            },
            isMonth() {
                return this.view === 'month';
            },
            inputDisabled() {
                return this.disabled || this.view === 'month';
            },
            clearVisible() {
                return this.hasClear && !utils$9.date.isZero(this.modelDate);
            },
            model: {
                get() {
                    if (utils$9.date.isZero(this.modelDate))
                        return ' '; /* avoid baseline problem */
                    if (this.view === 'month')
                        return utils$9.text.capitalize(this.modelDate.toLocaleString(monthLocale$1, { year: 'numeric', month: 'long' }));
                    else
                        return this.modelDate.toLocaleString(dateLocale$2, { year: 'numeric', month: '2-digit', day: '2-digit' });
                },
                set(str) {
                    let md = utils$9.date.parse(str, this.yearCutOff);
                    md = this.fitDate(md);
                    if (utils$9.date.isZero(md)) {
                        this.updateModel(md);
                        this.isOpen = false;
                    }
                    else {
                        this.setDate(md);
                    }
                }
            }
        },
        mounted() {
            this.viewDate = this.modelDate;
            popup$4.registerPopup(this.$el);
            this.$el._close = this.__clickOutside;
        },
        beforeUnmount() {
            popup$4.unregisterPopup(this.$el);
        }
    }));

    // combobox (Net48 components/combobox.js), unchanged in behaviour: over control; a select of itemsSource (or its own
    // options in the slot), grouped by groupby; the value is always the itemsSource's element. The server writes it as
    // <select is="vue:combobox"> (ComboBox.cs). v-if beside v-for tests groupby, a prop: Vue 3's order (v-if first)
    // gives the same.
    const utils$8 = require$1('std:utils');
    const comboBoxTemplate = `<div :class="cssClass()" v-lazy="itemsSource" :test-id="testId">
	<label v-if="hasLabel"><span v-text="label"/><slot name="hint"/><slot name="link"></slot></label>
	<div class="input-group">
		<div class="select-wrapper" :disabled="disabled" >
			<div v-text="getWrapText()" class="select-text" ref="wrap" :class="wrapClass"/>
			<span class="caret"/>
		</div>
		<select v-focus v-model="cmbValue" :disabled="disabled" :tabindex="tabIndex" ref="sel" :title="getWrapText()">
			<slot>
				<optgroup v-for="(grp, grpIndex) in itemsSourceGroup" :key="grpIndex" v-if="groupby"
					:label="grp.name">
					<option v-for="(cmb, cmbIndex) in grp.items" :key="grpIndex + '_' + cmbIndex"
						v-text="getName(cmb, true)" :value="getValue(cmb)" :class="getClass(cmb)"></option>
				</optgroup>
				<option v-for="(cmb, cmbIndex) in itemsSource" :key="cmbIndex" v-if="!groupby"
					v-text="getName(cmb, true)" :value="getValue(cmb)" :class="getClass(cmb)"></option>
			</slot>
		</select>
		<validator :invalid="invalid" :errors="errors" :options="validatorOptions"></validator>
	</div>
	<slot name="popover"></slot>
	<span class="descr" v-if="hasDescr" v-text="description"></span>
</div>
`;
    platform$3.vocabulary.component('combobox', vue.defineComponent({
        extends: control,
        template: comboBoxTemplate,
        props: {
            prop: String,
            item: {
                type: Object, default() { return {}; }
            },
            itemsSource: {
                type: Array, default() { return []; }
            },
            itemToValidate: Object,
            propToValidate: String,
            nameProp: String,
            valueProp: String,
            boldProp: String,
            cssClassProp: String,
            showvalue: Boolean,
            align: String,
            groupby: String
        },
        computed: {
            cmbValue: {
                get() {
                    const item = this.item;
                    if (this.itemsSource.length === 0 && item) {
                        const itemval = item[this.prop];
                        if (itemval && itemval.$empty && !itemval.$isEmpty)
                            itemval.$empty();
                    }
                    return this.getComboValue();
                },
                set(value) {
                    if (this.item)
                        this.item[this.prop] = value;
                }
            },
            itemsSourceGroup() {
                if (!this.groupby)
                    return this.itemsSource;
                const prop = this.groupby;
                const items = this.itemsSource;
                const set = new Set(items.map((x) => utils$8.eval(x, prop)));
                return Array.from(set).map(key => { return { name: key, items: items.filter((val) => key == utils$8.eval(val, prop)) }; });
            },
            wrapClass() {
                let cls = '';
                if (this.align && this.align !== 'left')
                    cls += 'text-' + this.align;
                return cls;
            }
        },
        methods: {
            getName(itm) {
                return this.nameProp ? utils$8.eval(itm, this.nameProp) : itm.$name;
            },
            getValue(itm) {
                return this.valueProp ? utils$8.eval(itm, this.valueProp) : itm;
            },
            getClass(itm) {
                if (this.boldProp)
                    return utils$8.eval(itm, this.boldProp) ? 'bold' : undefined;
                if (this.cssClassProp)
                    return utils$8.eval(itm, this.cssClassProp);
                return undefined;
            },
            getWrapText() {
                return this.showvalue ? this.getComboValue() : this.getText();
            },
            getComboValue() {
                const val = this.item ? this.item[this.prop] : null;
                if (!utils$8.isObjectExact(val))
                    return val;
                const vProp = this.valueProp || '$id';
                if (!(vProp in val))
                    return val;
                if (this.itemsSource.indexOf(val) !== -1) {
                    return val;
                }
                // always return value from ItemsSource
                return this.itemsSource.find((x) => x[vProp] === val[vProp]) || null;
            },
            getText() {
                const cv = this.getComboValue();
                if (cv === null)
                    return '';
                if (utils$8.isObjectExact(cv))
                    return this.getName(cv);
                if (this.itemsSource && this.itemsSource.length) {
                    const vProp = this.valueProp || '$id';
                    const ob = this.itemsSource.find((x) => x[vProp] === cv);
                    return ob ? this.getName(ob) : '';
                }
                else {
                    return this.getOptionsText(cv);
                }
            },
            getOptionsText(cv) {
                // get text from select directly.
                const sel = this.$refs.sel;
                if (!sel)
                    return '';
                const ops = sel.options;
                for (let i = 0; i < ops.length; i++) {
                    if ('' + ops[i].value === '' + cv)
                        return ops[i].text;
                }
                const si = sel.selectedIndex;
                if (si < 0 || si >= ops.length)
                    return '';
                return ops[si].text;
            }
        },
        mounted() {
            // litle hack. $refs are unavailable during init.
            const t = this.getText();
            if (t)
                this.$refs.wrap.innerText = t;
        }
    }));

    // a2-sheet, a2-sheet-section, a2-sheet-section-tree (Net48 components/sheet.js), unchanged in behaviour: a report's
    // table by named slots (columns, header, col-shadow, body, footer); a tree section renders its slot once per element
    // of the tree, walked depth first, a collapsed element's children skipped.
    // Vue 3: the tree is a functional component, a function of (props, { slots }); the slot's first vnode is taken as it
    // is (Vue 2 made it anew from tag, data and children); Vue.set is an assignment; the scope's functions are bound to
    // the scope (Vue 2's callers called them as its methods: tree.toggle()).
    const sheetTemplate = `
<table class="sheet" :class="cssClass">
	<slot name="columns"></slot>
	<thead>
		<slot name="header"></slot>
	</thead>
	<slot name="col-shadow"></slot>
	<slot name="body"></slot>
	<tfoot>
		<slot name="footer"></slot>
	</tfoot>
</table>
`;
    const sheetSectionTemplate = `
<tbody>
	<slot></slot>
</tbody>
`;
    function* traverse(item, prop, lev) {
        if (prop in item) {
            const arr = item[prop];
            for (let i = 0; i < arr.length; i++) {
                const elem = arr[i];
                elem.$level = lev;
                yield elem;
                if (!elem.$collapsed) {
                    const newprop = elem._meta_.$items || prop;
                    yield* traverse(elem, newprop, lev + 1);
                }
            }
        }
    }
    platform$3.vocabulary.component('a2-sheet', vue.defineComponent({
        template: sheetTemplate,
        props: {
            stale: Boolean
        },
        computed: {
            cssClass() {
                return {
                    'sheet-stale': this.stale
                };
            }
        }
    }));
    platform$3.vocabulary.component('a2-sheet-section', vue.defineComponent({
        template: sheetSectionTemplate
    }));
    const sheetSectionTree = (props, { slots }) => {
        const prop = props.propName;
        const source = props.itemsSource;
        if (!source)
            return null;
        if (!prop)
            return null;
        const slot = slots.default;
        if (!slot)
            return null;
        const compArr = [];
        for (const v of traverse(source, prop, 1)) {
            const scope = { item: v };
            scope.hasChildren = function () {
                const np = this.item._meta_.$items || prop;
                const chElems = this.item[np];
                return chElems && chElems.length > 0;
            }.bind(scope);
            scope.toggle = function () {
                const clpsed = this.item.$collapsed || false;
                this.item.$collapsed = !clpsed;
            }.bind(scope);
            scope.cssClass = function () {
                let cls = '';
                if (this.hasChildren())
                    cls += 'has-children';
                if (this.item.$collapsed)
                    cls += ' collapsed';
                cls += ' lev-' + this.item.$level;
                return cls;
            }.bind(scope);
            scope.rowCssClass = function (mark) {
                let cls = mark || '';
                if (this.hasChildren())
                    cls += ` group gl-${this.item.$level}`;
                if (this.item.$collapsed)
                    cls += ' collapsed';
                return cls;
            }.bind(scope);
            scope.indentCssClass = function () {
                return 'indent lev-' + this.item.$level;
            }.bind(scope);
            compArr.push(slot(scope)[0]);
        }
        return vue.h('tbody', {}, compArr);
    };
    sheetSectionTree.props = ['itemsSource', 'propName'];
    platform$3.vocabulary.component('a2-sheet-section-tree', sheetSectionTree);

    // a2-blob-image (Net48 components/blobimage.js), unchanged in behaviour: the image of a blob reference (item[prop]:
    // always an object, $id 0 when empty), shown by its url (std:url blobUrl); a file chosen or dropped goes up to
    // _blob/<url>/<prop>, and the answer (Id, Token, Name, Mime) is written into the reference.
    const url$3 = require$1('std:url');
    const http = require$1('std:http');
    const tools = require$1('std:tools');
    const locale$5 = window.$$locale;
    platform$3.vocabulary.component('a2-blob-image', vue.defineComponent({
        template: `
<div class="a2-image">
	<img v-if="hasImage" :src="href" :style="cssStyle"/>
	<a class="remove-image" v-if="hasImage" @click.prevent="removeImage">&#x2715;</a>
	<label v-else :class="uploadClass" :style="uploadStyle"
			@dragover.prevent="hover = true" @dragleave.prevent="hover = false">
		<input type="file" accept="image/*" @change="uploadImage"/>
		<i class="ico ico-image"></i>
		<span class="upload-tip" v-text="tip"></span>
	</label>
</div>
`,
        props: {
            url: String,
            item: Object,
            prop: String,
            width: String,
            height: String
        },
        data() {
            return {
                hover: false
            };
        },
        computed: {
            blob() {
                // RefId: always an object; an empty reference has $id = 0
                return this.item[this.prop];
            },
            href() {
                return url$3.blobUrl(this.blob);
            },
            hasImage() {
                return !!this.href;
            },
            tip() {
                return locale$5.$ClickToDownloadPicture;
            },
            cssStyle() {
                return { maxWidth: this.width, maxHeight: this.height };
            },
            uploadStyle() {
                const w = { width: this.width, height: this.height };
                if (!w.width)
                    w.width = w.height;
                if (!w.height)
                    w.height = w.width;
                return w;
            },
            uploadClass() {
                return 'file-upload' + (this.hover ? ' hover' : '');
            }
        },
        methods: {
            removeImage() {
                this.blob.$empty();
            },
            uploadImage(ev) {
                this.hover = false;
                const input = ev.target;
                const file = input.files[0];
                if (!file)
                    return;
                const fd = new FormData();
                fd.append('file', file, file.name);
                // the door: endpoint from Url, key = prop
                const uploadUrl = url$3.combine(window.$$rootUrl, '_blob', this.url, this.prop);
                http.upload(uploadUrl, fd).then((result) => {
                    // result = { Id, Token, Name, Mime }
                    input.value = '';
                    const b = this.blob;
                    b[b._meta_.$id] = result.Id;
                    b[b._meta_.$token] = result.Token;
                    if ('Name' in b)
                        b.Name = result.Name;
                    if ('Mime' in b)
                        b.Mime = result.Mime;
                }).catch((msg) => {
                    input.value = '';
                    if (msg.indexOf('UI:') === 0)
                        tools.alert(msg.substring(3).replace('\\n', '\n'));
                    else
                        alert(msg);
                });
            }
        }
    }));

    // a2-blob-preview (Net48 components/blobpreview.js), unchanged in behaviour: a blob reference (value: an object,
    // $id 0 when empty) previewed by its Mime: an image, a pdf object, else a link to it; nothing to show, a note.
    const url$2 = require$1('std:url');
    const locale$4 = window.$$locale;
    platform$3.vocabulary.component('a2-blob-preview', vue.defineComponent({
        template: `<div class="a2-file-preview">
	<img v-if="isImage" :src="href" :style="cssStyle"/>
	<object v-else-if="isPdf" type="application/pdf" :data="href"
		:width="pdfWidth" :height="height" :style="cssStyle"></object>
	<div v-else-if="hasBlob" class="no-preview">
		<span class="ico ico-file-preview"></span>
		<a :href="href" v-text="name"></a>
	</div>
	<div v-else class="no-preview">
		<span class="ico ico-file-preview"></span>
		<span v-text="unavailable"></span>
	</div>
</div>`,
        props: {
            value: Object,
            width: String,
            height: String
        },
        computed: {
            val() {
                return this.value;
            },
            // RefId: an object; an empty reference has $id = 0
            hasBlob() {
                return !!(this.val && this.val.$id);
            },
            mime() {
                return this.hasBlob ? (this.val.Mime || '') : '';
            },
            name() {
                return this.hasBlob ? this.val.Name : '';
            },
            // svg too: <img> runs no script, and the server never serves it inline
            isImage() {
                return this.mime.startsWith('image/');
            },
            isPdf() {
                return this.mime === 'application/pdf';
            },
            href() {
                if (!this.hasBlob)
                    return undefined;
                return url$2.blobUrl(this.val);
            },
            pdfWidth() {
                return this.width || '100%';
            },
            unavailable() {
                return locale$4.$PreviewIsUnavailable;
            },
            cssStyle() {
                const r = {};
                if (this.width)
                    r.maxWidth = this.width;
                if (this.height)
                    r.maxHeight = this.height;
                return r;
            }
        }
    }));

    // a2-list (Net48 components/list.js), unchanged in behaviour: the elements of an array by the slot 'items', grouped
    // by a property if asked, selected by mouse and arrows; the first, the last or the model's element selected at start.
    // Vue 3: $root was the page (base.js): its $modelInfo is the page's controller's (inject $page).
    const utils$7 = require$1('std:utils');
    const locale$3 = window.$$locale;
    platform$3.vocabulary.component('a2-list', vue.defineComponent({
        template: `
<ul class="a2-list" v-lazy="itemsSource">
	<template v-if="itemsSource">
		<li class="a2-list-item" tabindex="1" :class="cssClass(listItem)" v-for="(listItem, listItemIndex) in source" :key="listItemIndex"
				@mousedown="select(listItem)" @keydown="keyDown"
				ref="li" v-on:dblclick.prevent="doDblClick">
			<span v-if="listItem.__group" v-text="listItem.__group"></span>
			<slot name="items" :item="listItem" v-if="!listItem.__group"/>
		</li>
		<div class="list-empty" v-if="$isEmpty">
			<slot name="empty" />
		</div>
	</template>
	<template v-else>
		<slot />
	</template>
</ul>
`,
        inject: {
            page: { from: '$page', default: null }
        },
        props: {
            itemsSource: Array,
            autoSelect: String,
            mark: String,
            markStyle: String,
            command: Function,
            selectable: {
                type: Boolean, default: true
            },
            hover: {
                type: Boolean, default: true
            },
            groupBy: String,
            doubleclick: Function
        },
        computed: {
            items() {
                return this.itemsSource;
            },
            selectedSource() {
                let src = this.items;
                if (!src)
                    return null;
                if (src.$origin)
                    src = src.$origin;
                return src.$selected;
            },
            source() {
                if (!this.groupBy)
                    return this.items;
                const grmap = {};
                for (const itm of this.items) {
                    let key = utils$7.eval(itm, this.groupBy);
                    if (utils$7.isDate(key))
                        key = utils$7.format(key, 'Date');
                    if (!utils$7.isDefined(key))
                        key = '';
                    if (key === '')
                        key = locale$3.$Unknown || 'Unknown';
                    if (!(key in grmap)) {
                        grmap[key] = {
                            group: key,
                            items: []
                        };
                    }
                    grmap[key].items.push(itm);
                }
                const rarr = [];
                for (const key in grmap) {
                    const me = grmap[key];
                    rarr.push(Object.assign({}, me, { __group: me.group, __count: me.items.length }));
                    for (const e of me.items) {
                        rarr.push(e);
                    }
                }
                return rarr;
            },
            $isEmpty() {
                return this.items && this.items.length === 0;
            }
        },
        methods: {
            cssClass(item) {
                const getMark = (el) => {
                    if (!this.mark)
                        return '';
                    let cls = utils$7.eval(el, this.mark);
                    if (this.markStyle === 'row')
                        cls += ' no-marker';
                    else if (this.markStyle === 'marker')
                        cls += ' no-background';
                    return cls;
                };
                if (this.groupBy && item.__group)
                    return 'group' + (this.mark ? ' mark' : '');
                return (item.$selected ? 'active ' : ' ') + getMark(item);
            },
            select(item) {
                if (!this.selectable)
                    return;
                if (item.$select)
                    item.$select();
            },
            selectStatic() {
                alert('yet not implemented');
                console.dir(this);
            },
            selectFirstItem() {
                if (!this.autoSelect)
                    return;
                if (!this.selectable)
                    return;
                const src = this.items;
                if (src.$selected)
                    return; // already selected
                if (!src || !src.length)
                    return;
                if (this.autoSelect === 'first-item') {
                    // from source (not $origin!)
                    const fe = src[0];
                    this.select(fe);
                    return;
                }
                else if (this.autoSelect === 'last-item') {
                    // from source (not $origin!)
                    const fe = src[src.length - 1];
                    this.select(fe);
                    return;
                }
                else if (this.autoSelect === 'item-id') {
                    const rootId = this.page.$modelInfo.Id;
                    if (!utils$7.isDefined(rootId)) {
                        console.error('Id not found in Root.modelInfo');
                        return;
                    }
                    let fe = src[0];
                    if (rootId) {
                        fe = src.find((itm) => itm.$id === rootId);
                        if (!fe)
                            console.error(`Element with id=${rootId} not found`);
                    }
                    if (fe)
                        this.select(fe);
                }
            },
            keyDown(e) {
                const next = (delta) => {
                    let index = this.items.indexOf(this.selectedSource);
                    if (index === -1)
                        return;
                    index += delta;
                    if (index === -1)
                        return;
                    if (index < this.items.length)
                        this.select(this.items[index]);
                };
                switch (e.which) {
                    case 38: // up
                        next(-1);
                        break;
                    case 40: // down
                        next(1);
                        break;
                    case 36: // home
                    case 35: // end
                    case 33: // pgUp
                    case 34: // pgDn
                        break;
                    case 13: // Enter
                        if (utils$7.isFunction(this.command)) {
                            this.command();
                        }
                        break;
                    default:
                        return;
                }
                e.preventDefault();
                e.stopPropagation();
            },
            doDblClick($event) {
                $event.stopImmediatePropagation();
                if (this.doubleclick)
                    this.doubleclick();
            }
        },
        created() {
            this.selectFirstItem();
        },
        updated() {
            if (!this.selectedSource && this.autoSelect) {
                this.selectFirstItem();
            }
            const src = this.items;
            if (!src)
                return;
            const li = this.$refs.li;
            if (!li)
                return;
            setTimeout(() => {
                const ix = li.findIndex(itm => itm.classList.contains('active'));
                if (ix !== -1 && li && ix < li.length)
                    li[ix].scrollIntoViewCheck();
            }, 0);
        }
    }));

    // a2-color-picker (Net48 components/colorpicker.js), unchanged in behaviour: over control; item[prop] is one of the
    // tag colors, picked from a pane or stepped by the arrows. A popup. mounted's query = valueText is Net48's, a copy
    // from the selector that sets nothing used.
    const popup$3 = require$1('std:popup');
    const eventBus$6 = require$1('std:eventBus');
    const colorPickerTemplate = `<div class="color-picker" :class="cssClass()" :test-id="testId">
	<label v-if="hasLabel"><span v-text="label"/><slot name="hint"/><slot name="link"></slot></label>
	<div class="input-group">
		<div v-focus tabindex="0" @click.stop.prevent="toggle"
				@keydown="keydown" class="color-picker-wrapper">
			<span class="tag-label" :class="cmbValue" v-text="text"/>
			<span class="caret"/>
		</div>
		<validator :invalid="invalid" :errors="errors" :options="validatorOptions"></validator>
		<div class="color-picker-pane" v-show="isOpen">
			<div class="color-picker-list">
				<span class="tag-label" :class="itm" @mousedown.prevent="hit(itm)"
					v-for="(itm, ix) in items" :key="ix" v-text="text">
				</span>
			</div>
		</div>
	</div>
	<slot name="popover"></slot>
	<span class="descr" v-if="hasDescr" v-text="description"></span>
</div>
`;
    const colors = 'default|green|orange|cyan|red|purple|pink|gold|blue|salmon|seagreen|tan|magenta|lightgray|olive|teal';
    platform$3.vocabulary.component('a2-color-picker', vue.defineComponent({
        extends: control,
        props: {
            item: {
                type: Object, default() { return {}; }
            },
            prop: String,
            text: String
        },
        data() {
            return {
                isOpen: false
            };
        },
        template: colorPickerTemplate,
        computed: {
            cmbValue: {
                get() {
                    return this.item[this.prop];
                },
                set(val) {
                    this.item[this.prop] = val;
                }
            },
            items() { return colors.split('|'); }
        },
        methods: {
            keydown(event) {
                event.stopPropagation();
                const items = this.items;
                switch (event.which) {
                    case 40: { // down
                        event.preventDefault();
                        const ix = items.indexOf(this.cmbValue);
                        if (ix < items.length - 1)
                            this.cmbValue = items[ix + 1];
                        else
                            this.cmbValue = items[0];
                        break;
                    }
                    case 38: { // up
                        event.preventDefault();
                        const ix = items.indexOf(this.cmbValue);
                        if (ix > 0)
                            this.cmbValue = items[ix - 1];
                        else
                            this.cmbValue = items[items.length - 1];
                        break;
                    }
                }
            },
            toggle() {
                if (!this.isOpen) {
                    eventBus$6.$emit('closeAllPopups');
                }
                this.isOpen = !this.isOpen;
            },
            hit(itm) {
                this.item[this.prop] = itm;
                this.isOpen = false;
            },
            __clickOutside() {
                this.isOpen = false;
            }
        },
        mounted() {
            const self = this;
            popup$3.registerPopup(this.$el);
            self.query = self.valueText;
            this.$el._close = this.__clickOutside;
        },
        beforeUnmount() {
            popup$3.unregisterPopup(this.$el);
        }
    }));

    // a2-color-combobox (Net48 components/colorcombobox.js), unchanged in behaviour: over control; item[prop] is one of
    // itemsSource, shown as a tag-label of its colorProp, picked from a pane or stepped by the arrows. item[prop] is the
    // element itself (an object) or its valueProp. A popup.
    const popup$2 = require$1('std:popup');
    const eventBus$5 = require$1('std:eventBus');
    const platform$1 = require$1('std:platform');
    const utils$6 = require$1('std:utils');
    const colorComboboxTemplate = `<div class="color-picker" :class="cssClass()" :test-id="testId">
	<label v-if="hasLabel"><span v-text="label"/><slot name="hint"/><slot name="link"></slot></label>
	<div class="input-group">
		<div v-focus tabindex="0" @click.stop.prevent="toggle"
				@keydown="keydown" class="color-picker-wrapper">
			<span class="tag-label" :class="color" v-text="text"/>
			<span class="caret"/>
		</div>
		<validator :invalid="invalid" :errors="errors" :options="validatorOptions"></validator>
		<div class="color-picker-pane" v-show="isOpen">
			<div class="color-picker-list">
				<span class="tag-label" :class="itemClass(itm)" @mousedown.prevent="hit(itm)"
					v-for="(itm, ix) in itemsSource" :key="ix" v-text="itemText(itm)">
				</span>
			</div>
		</div>
	</div>
	<slot name="popover"></slot>
	<span class="descr" v-if="hasDescr" v-text="description"></span>
</div>
`;
    platform$3.vocabulary.component('a2-color-combobox', vue.defineComponent({
        extends: control,
        props: {
            item: {
                type: Object, default() { return {}; }
            },
            itemsSource: Array,
            prop: String,
            nameProp: String,
            colorProp: String,
            valueProp: String,
            outline: Boolean
        },
        data() {
            return {
                isOpen: false
            };
        },
        template: colorComboboxTemplate,
        computed: {
            text() {
                const cv = this.cmbValue;
                return cv ? cv[this.nameProp] : '';
            },
            color() {
                const cv = this.cmbValue;
                let clr = cv ? (cv[this.colorProp] || 'transparent') : 'transparent';
                if (this.outline)
                    clr += ' outline';
                return clr;
            },
            cmbValue: {
                get() {
                    const v = this.item[this.prop];
                    if (utils$6.isObjectExact(v))
                        return v;
                    return this.itemsSource.find((s) => s[this.valueProp] === v);
                },
                set(val) {
                    const v = this.item[this.prop];
                    if (utils$6.isObjectExact(v))
                        platform$1.set(this.item, this.prop, val);
                    else
                        this.item[this.prop] = val[this.valueProp];
                }
            }
        },
        methods: {
            itemText(itm) {
                return itm[this.nameProp];
            },
            itemClass(itm) {
                return itm[this.colorProp] + (this.outline ? ' outline' : '');
            },
            keydown(event) {
                event.stopPropagation();
                if (this.disabled)
                    return;
                const items = this.itemsSource;
                switch (event.which) {
                    case 40: { // down
                        event.preventDefault();
                        const ix = items.indexOf(this.cmbValue);
                        if (ix < items.length - 1)
                            this.cmbValue = items[ix + 1];
                        else
                            this.cmbValue = items[0];
                        break;
                    }
                    case 38: { // up
                        event.preventDefault();
                        const ix = items.indexOf(this.cmbValue);
                        if (ix > 0)
                            this.cmbValue = items[ix - 1];
                        else
                            this.cmbValue = items[items.length - 1];
                        break;
                    }
                }
            },
            toggle() {
                if (this.disabled)
                    return;
                if (!this.isOpen) {
                    eventBus$5.$emit('closeAllPopups');
                }
                this.isOpen = !this.isOpen;
            },
            hit(itm) {
                this.cmbValue = itm;
                this.isOpen = false;
            },
            __clickOutside() {
                this.isOpen = false;
            }
        },
        mounted() {
            popup$2.registerPopup(this.$el);
            this.$el._close = this.__clickOutside;
        },
        beforeUnmount() {
            popup$2.unregisterPopup(this.$el);
        }
    }));

    // a2-pdfreport-viewer (Net48 components/report.js), unchanged in behaviour: the report as pdf in an <object>, from
    // report/show/<the argument's id>?base=<url>&rep=<report>.
    const url$1 = require$1('std:url');
    const utils$5 = require$1('std:utils');
    platform$3.vocabulary.component('a2-pdfreport-viewer', vue.defineComponent({
        template: `
<div class="a2-pdfreport-viewer">
	<object type="application/pdf" :data="source" width="100%" height="100%"/>
</div>
`,
        props: {
            url: String,
            argument: Object,
            report: String
        },
        computed: {
            source() {
                const root = window.$$rootUrl;
                let arg = this.argument || 0;
                if (utils$5.isObjectExact(this.argument)) {
                    arg = this.argument.$id || 0;
                }
                return url$1.combine(root, 'report/show', arg) + url$1.makeQueryString({ base: this.url, rep: this.report });
            }
        }
    }));

    // tree-grid (Net48 components/treegrid.js), unchanged in behaviour: a tree as table rows, an element's children
    // (item) shown while it is $expanded; the rows, header and columns are the page's slots, given the grid as 'that'.
    // Vue 3: <col> is void; $parent.$emit('sort') was heard by a collection view's $on('sort') only, else by none:
    // $parent.doSort, when the parent has one.
    const utils$4 = require$1('std:utils');
    const eventBus$4 = require$1('std:eventBus');
    const debouncedUpdate = utils$4.debounce((pane) => {
        if (!pane)
            return;
        const elem = pane.querySelector('.active');
        if (!elem)
            return;
        if (elem && elem.scrollIntoViewCheck)
            elem.scrollIntoViewCheck();
    }, 300);
    const gridTemplate = `
<table v-lazy="root">
	<colgroup><col class="fit c-m" v-if=isMarkCell /><slot name="columns" v-bind:that="that"></slot></colgroup>
	<thead><tr>
		<th class="c-m" v-if=isMarkCell :class=gridLines></th>
		<slot name="header" v-bind:that="that"></slot>
	</tr></thead>
	<tbody ref="pane">
		<tr v-for="(itm, ix) in rows" :class="rowClass(itm)"
				@click.stop="select(itm)" v-on:dblclick.prevent="dblClick($event, itm)">
			<td class="c-m" v-if=isMarkCell :class="rowMarkClass(itm)"></td>
			<slot name="row" v-bind:itm="itm.elem" v-bind:that="that"></slot>
		</tr>
	</tbody>
</table>
`;
    platform$3.vocabulary.component('tree-grid', vue.defineComponent({
        template: gridTemplate,
        props: {
            root: [Object, Array],
            item: String,
            folderStyle: String,
            doubleclick: Function,
            isFolder: String,
            mark: String,
            markStyle: String,
            gridLines: String
        },
        computed: {
            rows() {
                const arr = [];
                const collect = (pa, lev) => {
                    for (let i = 0; i < pa.length; i++) {
                        const el = pa[i];
                        const ch = el[this.item];
                        arr.push({ elem: el, level: lev });
                        if (ch && el.$expanded)
                            collect(ch, lev + 1);
                    }
                };
                const root = this.root;
                if (Array.isArray(root))
                    collect(root, 0);
                else
                    collect(root[this.item], 0);
                return arr;
            },
            sortColumn() {
                const sort = {
                    dir: '',
                    order: ''
                };
                const mi = this.root.$ModelInfo;
                if (!mi)
                    return sort;
                sort.dir = (mi.SortDir || '').toLowerCase();
                sort.order = (mi.SortOrder || '').toLowerCase();
                return sort;
            },
            that() {
                return this;
            },
            isMarkCell() {
                return this.markStyle === 'marker' || this.markStyle === 'both';
            },
            isMarkRow() {
                return this.markStyle === 'row' || this.markStyle === 'both';
            }
        },
        methods: {
            toggle(itm) {
                itm.$select(this.root);
                if (itm.$expand) {
                    if (!itm.$expanded)
                        itm.$expand();
                    else
                        itm.$expanded = false;
                }
                else
                    itm.$expanded = !itm.$expanded;
            },
            select(itm) {
                eventBus$4.$emit('closeAllPopups');
                itm.elem.$select(this.root);
            },
            dblClick(evt) {
                eventBus$4.$emit('closeAllPopups');
                evt.stopImmediatePropagation();
                window.getSelection().removeAllRanges();
                if (this.doubleclick)
                    this.doubleclick();
            },
            hasChildren(itm) {
                const hc = utils$4.isDefined(itm.$hasChildren) && itm.$hasChildren;
                const ch = itm[this.item];
                return hc || (ch && ch.length > 0);
            },
            isRowFolder(elm) {
                if (this.hasChildren(elm))
                    return true;
                if (this.isFolder)
                    return utils$4.simpleEval(elm, this.isFolder);
                return '';
            },
            rowMarkClass(itm) {
                if (this.isMarkRow && this.mark)
                    return utils$4.simpleEval(itm.elem, this.mark) + ' ' + this.gridLines;
                return this.gridLines;
            },
            rowMarker(itm) {
                if (this.isMarkRow && this.mark)
                    return utils$4.simpleEval(itm.elem, this.mark);
                return '';
            },
            rowClass(itm) {
                let cls = `lev lev-${itm.level} ` + this.rowMarker(itm);
                if (itm.elem.$selected)
                    cls += ' active';
                if (this.isRowFolder(itm.elem) && this.folderStyle !== 'none')
                    cls += ' ' + this.folderStyle;
                return cls;
            },
            toggleClass(itm) {
                return itm.$expanded ? 'expanded' : 'collapsed';
            },
            columnClass(sort, fitcol) {
                const col = this.sortColumn;
                const cls = {
                    sorted: false,
                    fit: fitcol
                };
                if (sort.toLowerCase() === col.order) {
                    cls.sorted = true;
                }
                return cls;
            },
            headerClass(sort) {
                const col = this.sortColumn;
                if (col.order === (sort || '').toLowerCase())
                    return col.dir;
                return null;
            },
            doSort(prop) {
                prop = (prop || '').toLowerCase();
                const parent = this.$parent;
                if (parent && parent.doSort)
                    parent.doSort(prop);
            }
        },
        updated() {
            debouncedUpdate(this.$refs['pane']);
        }
    }));

    // tree-view (Net48 components/treeview.js), unchanged in behaviour: a tree as nested lists, tree-item per element,
    // its children (options.subitems) shown while it is $expanded; a click selects ($select over the root items) or
    // toggles a folder that options.folderSelect does not let select; options name the label, icon, title, css props.
    // Vue 3: tree-item resolves itself by its name; _toggling, a data key Vue 2 did not proxy (so not reactive), is set
    // on the instance in created; $children → ref 'items'.
    const utils$3 = require$1('std:utils');
    const eventBus$3 = require$1('std:eventBus');
    const platform = require$1('std:platform');
    //stop for toggle is required!
    const treeItemComponent = vue.defineComponent({
        name: 'tree-item',
        template: `
<li @click.stop.prevent="doClick(item)" :title=title v-on:dblclick.stop.prevent="doDblClick(item)"
	v-show=isItemVisible
	:class="[cssClass, {expanded: isExpanded, collapsed:isCollapsed, active:isItemSelected, folder:isFolder, group: isItemGroup}]" >
	<div :class="{overlay:true, 'no-icons': !options.hasIcon}">
		<a class="toggle" v-if="isFolder" href @click.stop.prevent=toggle></a>
		<span v-else class="toggle"/>
		<i v-if="options.hasIcon" :class="iconClass"/>
		<a v-if="hasLink(item)" :href=dataHref tabindex="-1" v-text="item[options.label]" :class="{'no-wrap':!options.wrapLabel }"/>
		<span v-else v-text="item[options.label]" :class="{'tv-folder':true, 'no-wrap':!options.wrapLabel}"/>
	</div>
	<ul v-if=isFolder v-show=isExpanded>
		<tree-item v-for="(itm, index) in item[options.subitems]" :options="options"
			:key="index" :item="itm" :click="click" :doubleclick="doubleclick" :get-href="getHref" :is-active="isActive" :expand="expand" :root-items="rootItems">
		</tree-item>
	</ul>
</li>
`,
        props: {
            item: { type: Object, required: true },
            options: { type: Object, required: true },
            rootItems: Array,
            /* callbacks */
            click: Function,
            expand: Function,
            isActive: Function,
            isGroup: Function,
            getHref: Function,
            doubleclick: Function
        },
        methods: {
            isFolderSelect(item) {
                const fs = this.options.folderSelect;
                if (utils$3.isFunction(fs))
                    return fs(item);
                return !!this.options.folderSelect;
            },
            doClick(item) {
                eventBus$3.$emit('closeAllPopups');
                if (this.isFolder && !this.isFolderSelect(item))
                    this.toggle();
                else if ("$select" in item)
                    item.$select(this.rootItems);
                else if (this.click)
                    this.click(item);
            },
            doDblClick(item) {
                eventBus$3.$emit('closeAllPopups');
                if (this.isFolder && !this.isFolderSelect(item))
                    return;
                if (this.doubleclick)
                    this.doubleclick();
            },
            hasLink(item) {
                return !this.isFolder || this.isFolderSelect(item);
            },
            toggle() {
                // toggle with stop!
                eventBus$3.$emit('closeAllPopups');
                if (!this.isFolder)
                    return;
                const self = this;
                self._toggling = true;
                this.expandItem(!this.item.$expanded);
                if (this.expand) {
                    this.expand(this.item, this.options.subitems);
                }
                this.$nextTick(() => {
                    self._toggling = false;
                });
            },
            expandItem(val) {
                platform.set(this.item, '$expanded', val);
            },
            openElem() {
                if (!this.isFolder)
                    return;
                this.expandItem(true);
            }
        },
        computed: {
            isFolder() {
                if (utils$3.isDefined(this.item.$hasChildren) && this.item.$hasChildren)
                    return true;
                if (utils$3.isDefined(this.options.isFolder))
                    return this.item[this.options.isFolder];
                const ch = this.item[this.options.subitems];
                return ch && ch.length;
            },
            isItemVisible() {
                const iv = this.options.isVisible;
                if (!iv)
                    return true;
                return this.item[iv];
            },
            isExpanded() {
                return this.isFolder && this.item.$expanded;
            },
            isCollapsed() {
                return this.isFolder && !this.item.$expanded;
            },
            title() {
                let t = this.item[this.options.title];
                if (!t)
                    t = this.item[this.options.label];
                return t;
            },
            isItemSelected() {
                if (this.item && "$selected" in this.item)
                    return this.item.$selected;
                return this.isActive && this.isActive(this.item);
            },
            isItemGroup() {
                const gp = this.options ? this.options.isGroup : undefined;
                if (gp)
                    return utils$3.eval(this.item, gp);
                else
                    return this.isGroup && this.isGroup(this.item);
            },
            iconClass() {
                const icons = this.options.staticIcons;
                if (icons)
                    return "ico ico-" + (this.isFolder ? icons[0] : icons[1]);
                if (this.options.icon) {
                    const icon = this.item[this.options.icon];
                    return icon ? "ico ico-" + (icon || 'empty') : '';
                }
                return undefined;
            },
            dataHref() {
                return this.getHref ? this.getHref(this.item) : '';
            },
            cssClass() {
                if (!this.options)
                    return undefined;
                const xname = this.options.xtraClass;
                if (!xname)
                    return undefined;
                return this.item[xname] || undefined;
            }
        },
        watch: {
            isItemSelected(newVal) {
                const el = this.$el;
                if (newVal && el.scrollIntoViewCheck)
                    el.scrollIntoViewCheck();
            }
        },
        updated() {
            // open expanded when reloaded
            if (!this._toggling && this.options.initialExpand)
                this.item.$expanded = true;
            if (this.item.$expanded) {
                if (this.item.$hasChildren) {
                    const arr = this.item[this.options.subitems];
                    if (!arr.$loaded) {
                        this.expandItem(false);
                    }
                }
            }
        },
        created() {
            this._toggling = false;
            if (this.options.initialExpand)
                platform.set(this.item, '$expanded', true);
        }
    });
    platform$3.vocabulary.component('tree-view', vue.defineComponent({
        components: {
            'tree-item': treeItemComponent
        },
        template: `
<ul class="tree-view">
	<tree-item v-for="(itm, index) in items" :options="options" :get-href="getHref"
		:item="itm" :key="index" ref="items"
		:click="click" :doubleclick="doubleclick" :is-active="isActive" :is-group="isGroup" :expand="expand" :root-items="items">
	</tree-item>
	<slot name=contextmenu></slot>
</ul>`,
        props: {
            options: Object,
            items: Array,
            isActive: Function,
            isGroup: Function,
            click: Function,
            expand: Function,
            autoSelect: String,
            getHref: Function,
            expandFirstItem: Boolean,
            doubleclick: Function
        },
        computed: {
            isSelectFirstItem() {
                return this.autoSelect === 'first-item';
            }
        },
        watch: {
            items() {
                this.doExpandFirst();
            }
        },
        methods: {
            selectFirstItem() {
                if (!this.isSelectFirstItem)
                    return;
                const itms = this.items;
                if (!itms.length)
                    return;
                let fe = null;
                if (this.options && this.options.isVisible) {
                    const vi = this.options.isVisible;
                    fe = itms.$find((x) => x[vi]);
                }
                else
                    fe = itms[0];
                if (!fe)
                    return;
                if (fe.$select)
                    fe.$select(this.items);
            },
            doExpandFirst() {
                if (!this.expandFirstItem)
                    return;
                this.$nextTick(() => {
                    const children = this.$refs['items'];
                    if (!children)
                        return;
                    children.forEach((val) => {
                        if (val && val.openElem) {
                            val.openElem();
                        }
                    });
                });
            }
        },
        created() {
            this.selectFirstItem();
            this.doExpandFirst();
        },
        updated() {
            if (this.isSelectFirstItem && !this.items.$selected) {
                this.selectFirstItem();
            }
        }
    }));

    // a2-text-editor, a2-text-view (A2v10.Core Web/A2v10.Core.Web.Site/Scripts/texteditor.js; not in Net48), unchanged
    // in behaviour. A comment: own contenteditable, the text is markdown of three elements — \n, **bold**, ![name](src).
    // Decisions — A2v10.Core design/texteditor.md.
    // Vue 3: a hook's this was vm.$root.$data, the page's model: the page's controller's $data (inject $page);
    // destroyed is unmounted. md, moving, gone, ranges are set on the instance in mounted: not reactive, as in Vue 2.
    const IMAGE_TYPES = ['image/png', 'image/jpeg', 'image/gif', 'image/webp'];
    function escapeHtml(s) {
        return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }
    function escapeMd(s) {
        return s.replace(/[\\*]/g, '\\$&');
    }
    // One rule for a link: the view makes it clickable, the editor highlights it. Trailing punctuation belongs to the sentence
    function* findLinks(s) {
        for (const m of s.matchAll(/https?:\/\/[^\s<>"]+/g))
            yield { index: m.index, url: m[0].replace(/[.,;:!?)]+$/, '') };
    }
    function textHtml(s, view) {
        if (!view)
            return escapeHtml(s);
        let out = '';
        let last = 0;
        for (const { index, url } of findLinks(s)) {
            out += escapeHtml(s.substring(last, index));
            out += `<a href="${escapeHtml(url)}" target="_blank" rel="noopener">${escapeHtml(url)}</a>`;
            last = index + url.length;
        }
        return out + escapeHtml(s.substring(last));
    }
    // The links of the editor's text, as ranges over its text nodes: the highlight paints them, a click opens them — one rule.
    // A link cut by bold into two text nodes is not a link here — accepted
    function linkRanges(el) {
        const ranges = [];
        const walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
        for (let n = walker.nextNode(); n; n = walker.nextNode()) {
            for (const { index, url } of findLinks(n.nodeValue)) {
                const r = new Range();
                r.setStart(n, index);
                r.setEnd(n, index + url.length);
                ranges.push(r);
            }
        }
        return ranges;
    }
    // by the rects, not by the caret: a caret lands on the nearest boundary, a click past the end of a line would open the link
    function hit(range, x, y) {
        return Array.from(range.getClientRects()).some(c => x >= c.left && x <= c.right && y >= c.top && y <= c.bottom);
    }
    // The CSS Custom Highlight API paints over the text: the DOM, the caret and undo stay as they are.
    // The registry is global, so editors share one Highlight, each holding its own ranges. No API — no highlight
    const HIGHLIGHT = 'a2-link';
    function linkHighlight() {
        const css = window.CSS;
        if (!css || !css.highlights)
            return null;
        let hl = css.highlights.get(HIGHLIGHT);
        if (!hl) {
            hl = new window.Highlight();
            css.highlights.set(HIGHLIGHT, hl);
        }
        return hl;
    }
    const IMAGE_RX = /^!\[((?:\\.|[^\]\\])*)\]\(([^)\s]+)\)/;
    // The one render: the view and the text loaded into the editor. Everything is escaped; the output is own markup only.
    // An image is an <img> only if imageUrl gives it a URL — otherwise text (a spy pixel); data-src keeps the stored form.
    // view: links clickable, an image is a link to its original (max-height cuts it) and lazy.
    // .loading — a fixed block with a spinner in the background until load/error (from Azure it takes seconds); on the img itself,
    // no wrapper: a wrapper in contenteditable breaks editing, and the editor loads images too
    function toHtml(md, imageUrl, view) {
        let out = '';
        let text = '';
        let bold = false;
        const flush = () => { out += textHtml(text, view); text = ''; };
        for (let i = 0; i < md.length;) {
            const ch = md[i];
            if (ch === '\\' && i + 1 < md.length) {
                text += md[i + 1];
                i += 2;
            }
            else if (ch === '*' && md[i + 1] === '*' && (bold || hasClose(md, i + 2))) {
                flush();
                out += bold ? '</b>' : '<b>';
                bold = !bold;
                i += 2;
            }
            else if (ch === '\n') {
                flush();
                out += '<br>';
                i++;
            }
            else if (ch === '!' && md[i + 1] === '[') {
                const m = IMAGE_RX.exec(md.substring(i));
                const url = m ? imageUrl(m[2]) : null;
                if (m && url) {
                    flush();
                    const name = m[1].replace(/\\(.)/g, '$1');
                    const href = escapeHtml(url);
                    const img = `<img src="${href}" data-src="${escapeHtml(m[2])}" alt="${escapeHtml(name)}" class="loading"`;
                    out += view ? `<a href="${href}" target="_blank" rel="noopener">${img} loading="lazy"></a>` : `${img}>`;
                    i += m[0].length;
                }
                else {
                    text += ch;
                    i++;
                }
            }
            else {
                text += ch;
                i++;
            }
        }
        flush();
        return out;
    }
    // A hook is called as a template's function is: this = the model of the page (as emptyPanelCallback). No hook — undefined
    function hook(vm, name, ...args) {
        const d = vm.delegates;
        return d && d[name] ? d[name].apply(vm.page.$data, args) : undefined;
    }
    // load/error do not bubble — the root sees them on capture. An error too: a broken image, not an endless spinner
    function loaded(ev) {
        const t = ev.target;
        if (t.tagName === 'IMG')
            t.classList.remove('loading');
    }
    // an unpaired ** is text
    function hasClose(md, i) {
        for (; i < md.length; i++) {
            if (md[i] === '\\')
                i++;
            else if (md[i] === '*' && md[i + 1] === '*')
                return true;
        }
        return false;
    }
    // Enter: Chrome makes a div, Firefox a br. The last br of a block is Chrome's stub of an empty line.
    // An img without data-src (still uploading — blob:) is not written: the model never holds a dead address.
    function toMarkdown(root) {
        let out = '';
        let breakAfterBlock = false;
        const emit = (s) => {
            if (breakAfterBlock) {
                out += '\n';
                breakAfterBlock = false;
            }
            out += s;
        };
        const walk = (node, inBold) => {
            for (let n = node.firstChild; n; n = n.nextSibling) {
                if (n.nodeType === Node.TEXT_NODE) {
                    if (n.nodeValue)
                        emit(escapeMd(n.nodeValue.replace(/\u00a0/g, ' ')));
                    continue;
                }
                if (n.nodeType !== Node.ELEMENT_NODE)
                    continue;
                const e = n;
                switch (e.tagName) {
                    case 'BR':
                        if (!e.nextSibling && (node.tagName === 'DIV' || node.tagName === 'P'))
                            break;
                        emit('\n');
                        break;
                    case 'B':
                    case 'STRONG': {
                        if (inBold) {
                            walk(e, true);
                            break;
                        }
                        emit('');
                        const start = out.length;
                        out += '**';
                        walk(e, true);
                        // an empty bold is skipped
                        if (out.length === start + 2)
                            out = out.substring(0, start);
                        else
                            out += '**';
                        break;
                    }
                    case 'DIV':
                    case 'P':
                        if (out)
                            out += '\n';
                        breakAfterBlock = false;
                        walk(e, inBold);
                        breakAfterBlock = true;
                        break;
                    case 'IMG': {
                        const src = e.getAttribute('data-src');
                        if (src)
                            emit(`![${(e.alt || '').replace(/[\\\]]/g, '\\$&')}](${src.replace(/[\s)]/g, encodeURIComponent)})`);
                        break;
                    }
                    default:
                        walk(e, inBold);
                }
            }
        };
        walk(root, false);
        return out.replace(/\n+$/, '');
    }
    function images(files) {
        return Array.from(files || []).filter(f => IMAGE_TYPES.includes(f.type));
    }
    // a screenshot from the clipboard is always image.png
    function named(file) {
        if (!/^image\.\w+$/i.test(file.name))
            return file;
        const d = new Date();
        const p = (n) => String(n).padStart(2, '0');
        const stamp = `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}-${p(d.getMinutes())}-${p(d.getSeconds())}`;
        return new File([file], `Знімок ${stamp}.${file.name.split('.').pop()}`, { type: file.type });
    }
    function caretAt(x, y) {
        let r = null;
        const doc = document;
        if (doc.caretPositionFromPoint) {
            const p = doc.caretPositionFromPoint(x, y);
            if (p) {
                r = document.createRange();
                r.setStart(p.offsetNode, p.offset);
            }
        }
        else if (doc.caretRangeFromPoint)
            r = doc.caretRangeFromPoint(x, y);
        if (!r)
            return;
        r.collapse(true);
        const sel = window.getSelection();
        sel.removeAllRanges();
        sel.addRange(r);
    }
    platform$3.vocabulary.component('a2-text-editor', vue.defineComponent({
        template: `
<div class="a2-text-editor" :contenteditable="disabled ? 'false' : 'true'" :data-placeholder="placeholder"
	@input="sync" @keydown="keydown" @focus="focus" @paste="paste" @click="open" @load.capture="loaded" @error.capture="loaded"
	@dragstart="dragInside(true)" @dragend="dragInside(false)" @drop="drop"></div>
`,
        inject: {
            page: { from: '$page', default: null }
        },
        props: {
            item: Object,
            prop: String,
            placeholder: String,
            disabled: Boolean,
            delegates: Object
        },
        computed: {
            value() {
                return this.item[this.prop] || '';
            }
        },
        watch: {
            // own input is never re-rendered — the caret stays
            value(v) {
                if (v === this.md)
                    return;
                this.render(v);
            }
        },
        methods: {
            loaded,
            imageUrl(src) {
                return hook(this, 'ImageUrl', src) || null;
            },
            render(md) {
                const self = this;
                self.md = md;
                this.$el.innerHTML = toHtml(md, src => this.imageUrl(src), false);
                this.highlight();
            },
            highlight() {
                const self = this;
                const hl = linkHighlight();
                if (!hl)
                    return;
                for (const r of self.ranges)
                    hl.delete(r);
                self.ranges = self.gone ? [] : linkRanges(this.$el);
                for (const r of self.ranges)
                    hl.add(r);
            },
            // Ctrl/⌘+click opens an image or a link, as a click in the view does; a plain click edits (places the caret,
            // selects an image). A disabled editor cannot be edited, so any click opens. An image still uploading
            // (blob:, no data-src) has nothing to open yet
            open(ev) {
                if (!(ev.ctrlKey || ev.metaKey) && !this.disabled)
                    return;
                let url = null;
                const t = ev.target;
                if (t.tagName === 'IMG') {
                    if (t.getAttribute('data-src'))
                        url = t.src;
                }
                else {
                    const link = linkRanges(this.$el).find(r => hit(r, ev.clientX, ev.clientY));
                    if (link)
                        url = link.toString();
                }
                if (!url)
                    return;
                ev.preventDefault();
                window.open(url, '_blank', 'noopener');
            },
            sync() {
                const self = this;
                if (self.gone)
                    return;
                const el = this.$el;
                // Chrome leaves a <br> in an emptied editor — :empty and the placeholder need it gone
                if (!el.textContent && !el.querySelector('img'))
                    el.innerHTML = '';
                self.md = toMarkdown(el);
                this.highlight();
                if (this.value !== self.md)
                    this.item[this.prop] = self.md;
            },
            focus() {
                // Ctrl+B → <b>, not <span style>
                document.execCommand('styleWithCSS', false, 'false');
            },
            keydown(ev) {
                if (!(ev.ctrlKey || ev.metaKey))
                    return;
                if (ev.key === 'Enter') {
                    ev.preventDefault();
                    hook(this, 'Submit');
                }
                else if (ev.code === 'KeyI' || ev.code === 'KeyU')
                    ev.preventDefault(); // italic and underline would be lost on save
            },
            canUpload() {
                const d = this.delegates;
                return !!(d && d.UploadImage);
            },
            paste(ev) {
                const dt = ev.clipboardData;
                if (!dt)
                    return;
                ev.preventDefault();
                const files = this.canUpload() ? images(dt.files) : [];
                if (files.length)
                    this.insertImages(files);
                else
                    this.insertText(dt.getData('text/plain'));
            },
            // dragstart fires only for a drag that began inside the editor
            dragInside(on) {
                this.moving = on;
            },
            drop(ev) {
                if (this.moving)
                    return; // a move inside the editor is the browser's
                ev.preventDefault();
                const dt = ev.dataTransfer;
                this.$el.focus();
                caretAt(ev.clientX, ev.clientY);
                const files = this.canUpload() ? images(dt.files) : [];
                if (files.length)
                    this.insertImages(files);
                else
                    this.insertText(dt.getData('text/plain'));
            },
            insertText(text) {
                // foreign HTML never enters
                if (text)
                    document.execCommand('insertText', false, text);
            },
            insertImages(files) {
                const items = files.map(f => {
                    const file = named(f);
                    return { file, url: URL.createObjectURL(file) };
                });
                // execCommand — for the native undo
                const html = items.map(x => `<img src="${x.url}" alt="${escapeHtml(x.file.name)}" class="uploading">`).join('');
                document.execCommand('insertHTML', false, html);
                for (const x of items)
                    this.upload(x);
            },
            async upload(x) {
                let src = null;
                try {
                    src = await hook(this, 'UploadImage', x.file);
                }
                catch (err) {
                    console.error(err);
                }
                const url = src ? this.imageUrl(src) : null;
                if (src && !url)
                    console.error(`a2-text-editor: ImageUrl knows nothing of '${src}' returned by UploadImage`);
                // erased while uploading — nothing to do
                const img = Array.from(this.$el.querySelectorAll('img')).find(i => i.getAttribute('src') === x.url);
                if (img) {
                    if (url) {
                        img.setAttribute('data-src', src);
                        img.src = url;
                        img.classList.remove('uploading');
                    }
                    else
                        img.remove();
                }
                URL.revokeObjectURL(x.url);
                // an attribute change fires no input
                this.sync();
            }
        },
        mounted() {
            // not reactive: own state of the DOM, never rendered
            const self = this;
            self.md = '';
            self.moving = false;
            self.gone = false;
            self.ranges = [];
            this.render(this.value);
        },
        unmounted() {
            this.gone = true;
            this.highlight();
        }
    }));
    platform$3.vocabulary.component('a2-text-view', vue.defineComponent({
        template: `<div class="a2-text-view" v-html="html" @load.capture="loaded" @error.capture="loaded"></div>`,
        inject: {
            page: { from: '$page', default: null }
        },
        // shown, not written — value, as a2-blob-preview
        props: {
            value: String,
            delegates: Object
        },
        computed: {
            html() {
                return toHtml(this.value || '', src => hook(this, 'ImageUrl', src) || null, true);
            }
        },
        methods: {
            loaded
        }
    }));

    // a2-tags, a2-tags-list, a2-tags-filter (Net48 components/tagscontrol.js), unchanged in behaviour: an element's tags
    // (item[prop], an array) picked from itemsSource; the tags shown; a filter's tags as ids joined by '-'. a2-tags and
    // a2-tags-filter are popups over control.
    const popup$1 = require$1('std:popup');
    const eventBus$2 = require$1('std:eventBus');
    const template$2 = `
<div class="tags-control" :class="cssClass()" :test-id="testId">
	<label v-if="hasLabel"><span v-text="label"/><slot name="hint"/><slot name="link"></slot></label>
	<div class="input-group" :class="{focus: isOpen}" @click.stop.prevent="toggle">
		<ul class="tags-items" v-if="hasItems">
			<li v-for="(itm, ix) in value" :key="ix" class="tag-body tag-md-close" :class="tagColor(itm)">
				<span v-text="tagName(itm)" :title="tagTitle(itm)"/>
				<button @click.stop.prevent="itm.$remove()" class="btn-close">×</button>
			</li>
		</ul>
		<div class="tags-placeholder" v-else v-text="placeholder"></div>
		<div class="tags-pane" v-if=isOpen>
			<ul class="tags-pane-items">
				<li v-for="(itm, ix) in actualItemsSource" :key="ix" class="tag-body tag-md" :class="tagColor(itm)"
					@click.stop.prevent="addTag(itm)">
					<span v-text="tagName(itm)" :title="tagTitle(itm)"/>
				</li>
			</ul>
			<div class="tags-settings" v-if="!disabled">
				<button class="btn-settings" v-text="settingsText" @click.stop.prevent=doSettings></button>
			</div>
		</div>
	</div>
	<slot name="popover"></slot>
	<span class="descr" v-if="hasDescr" v-text="description"></span>
</div>
`;
    const templateList = `
<div class="tags-list" :test-id="testId">
	<span v-for="(itm, ix) in itemsSource" :key="ix" class="tag-body tag-sm" :class="tagColor(itm)" v-text="tagName(itm)" :title="tagTitle(itm)"/>
</div>
`;
    const templateFilter = `
<div class="tags-control" :class="cssClass()" :test-id="testId">
	<label v-if="hasLabel"><span v-text="label"/><slot name="hint"/><slot name="link"></slot></label>
	<div class="input-group" :class="{focus: isOpen}" @click.stop.prevent="toggle">
		<ul class="tags-items" v-if="hasItems">
			<li v-for="(itm, ix) in valueList" :key="ix" class="tag-body tag-md-close" :class="tagColor(itm)">
				<span v-text="tagName(itm)" :title="tagTitle(itm)"/>
				<button @click.stop.prevent="removeTag(itm)" class="btn-close">×</button>
			</li>
		</ul>
		<div class="tags-placeholder" v-else v-text="placeholder"></div>
		<div class="tags-pane" v-if=isOpen>
			<ul class="tags-pane-items">
				<li v-for="(itm, ix) in actualItemsSource" :key="ix" class="tag-body tag-md" :class="tagColor(itm)"
						@click.stop.prevent="addTag(itm)">
					<span v-text="tagName(itm)" :title="tagTitle(itm)" />
				</li>
			</ul>
		</div>
	</div>
	<slot name="popover"></slot>
	<span class="descr" v-if="hasDescr" v-text="description"></span>
</div>
`;
    // the tag's name, color and title by the props naming them
    const tagMethods = {
        tagName(itm) {
            return itm[this.contentProp];
        },
        tagColor(itm) {
            return itm[this.colorProp];
        },
        tagTitle(itm) {
            return itm[this.titleProp];
        }
    };
    platform$3.vocabulary.component('a2-tags', vue.defineComponent({
        extends: control,
        props: {
            item: {
                type: Object, default() { return {}; }
            },
            prop: String,
            itemsSource: Array,
            contentProp: { type: String, default: 'Name' },
            colorProp: { type: String, default: 'Color' },
            titleProp: { type: String, default: 'Memo' },
            disabled: Boolean,
            settingsText: { type: String, default: 'Settings' },
            placeholder: String,
            settingsFunc: Function,
            settingsCommand: Function
        },
        data() {
            return {
                isOpen: false
            };
        },
        template: template$2,
        computed: {
            value() {
                return this.item[this.prop];
            },
            hasItems() {
                return this.value.length > 0;
            },
            actualItemsSource() {
                const val = this.value;
                return this.itemsSource.filter(x => !val.some((ix) => x.$id === ix.$id));
            }
        },
        methods: {
            ...tagMethods,
            addTag(itm) {
                if (this.disabled)
                    return;
                const val = this.value;
                if (val.some((x) => x.$id === itm.$id))
                    return; // already added
                this.item[this.prop].$append(itm);
            },
            toggle() {
                if (this.disabled)
                    return;
                if (!this.isOpen) {
                    eventBus$2.$emit('closeAllPopups');
                }
                this.isOpen = !this.isOpen;
            },
            doSettings() {
                const root = this.item.$root;
                if (this.settingsCommand)
                    this.settingsCommand.call(root);
                else if (this.settingsFunc)
                    this.settingsFunc.call(root, this.itemsSource);
            },
            __clickOutside() {
                this.isOpen = false;
            }
        },
        mounted() {
            popup$1.registerPopup(this.$el);
            this.$el._close = this.__clickOutside;
        },
        beforeUnmount() {
            popup$1.unregisterPopup(this.$el);
        }
    }));
    platform$3.vocabulary.component('a2-tags-list', vue.defineComponent({
        template: templateList,
        props: {
            testId: String,
            itemsSource: Array,
            contentProp: { type: String, default: 'Name' },
            colorProp: { type: String, default: 'Color' },
            titleProp: { type: String, default: 'Memo' }
        },
        methods: {
            ...tagMethods
        }
    }));
    platform$3.vocabulary.component('a2-tags-filter', vue.defineComponent({
        extends: control,
        props: {
            item: {
                type: Object, default() { return {}; }
            },
            prop: String,
            itemsSource: Array,
            contentProp: { type: String, default: 'Name' },
            colorProp: { type: String, default: 'Color' },
            titleProp: { type: String, default: 'Memo' },
            placeholder: String
        },
        data() {
            return {
                isOpen: false
            };
        },
        template: templateFilter,
        computed: {
            value() {
                return this.item[this.prop];
            },
            valueArray() {
                const v = this.value;
                return v ? v.split('-') : [];
            },
            valueList() {
                const va = this.valueArray;
                return this.itemsSource.filter(tag => va.indexOf('' + tag.$id) >= 0);
            },
            actualItemsSource() {
                const va = this.valueArray;
                return this.itemsSource.filter(tag => va.indexOf('' + tag.$id) < 0);
            },
            hasItems() {
                return !!this.value;
            }
        },
        methods: {
            ...tagMethods,
            addTag(itm) {
                if (this.disabled)
                    return;
                const va = this.valueArray;
                if (va.indexOf('' + itm.$id) >= 0)
                    return; // already added
                this.item[this.prop] = va.concat([itm.$id]).join('-');
            },
            removeTag(itm) {
                const va = this.valueArray.filter((x) => x != itm.$id);
                this.item[this.prop] = va.join('-');
            },
            toggle() {
                if (this.disabled)
                    return;
                if (!this.isOpen) {
                    eventBus$2.$emit('closeAllPopups');
                }
                this.isOpen = !this.isOpen;
            },
            __clickOutside() {
                this.isOpen = false;
            }
        },
        mounted() {
            popup$1.registerPopup(this.$el);
            this.$el._close = this.__clickOutside;
        },
        beforeUnmount() {
            popup$1.unregisterPopup(this.$el);
        }
    }));

    // popover (Net48 components/popover.js), unchanged in behaviour: a v-dropdown whose [toggle] is the title (icon,
    // content); its body is the slot and, with url, an include of /_popup/<url>/<arg> loaded while shown. hover shows it
    // on mouseover. A [close-dropdown] element inside closes it; tabindex="-1" on the toggle closes it on blur.
    // arg: Vue 2's `undefined` type is `null` (any).
    const template$1 = `
<div v-dropdown class="popover-wrapper" :style="{top: top}" :class="{show: isShowHover}" :title="title">
	<span toggle class="popover-title" :class=poClass :style=poStyle v-on:mouseover="mouseover"
		v-on:mouseout="mouseout" ><i v-if="hasIcon" :class="iconClass"></i> <span v-text="content"></span><slot name="badge"></slot></span>
	<div class="popup-body" :style="{width: width, left:offsetLeft}">
		<div class="arrow" :style="{left:offsetArrowLeft}"/>
		<div v-if="visible">
			<include :src="popoverUrl"/>
		</div>
		<slot />
	</div>
</div>
`;
    platform$3.vocabulary.component('popover', vue.defineComponent({
        template: template$1,
        data() {
            return {
                state: 'hidden',
                hoverstate: false,
                popoverUrl: ''
            };
        },
        props: {
            icon: String,
            url: String,
            content: [String, Number],
            title: String,
            width: String,
            top: String,
            hover: Boolean,
            offsetX: String,
            arg: null,
            lineClamp: Number
        },
        computed: {
            hasIcon() {
                return !!this.icon;
            },
            offsetLeft() {
                return this.offsetX || undefined;
            },
            offsetArrowLeft() {
                if (this.offsetX && this.offsetX.indexOf('-') === 0)
                    return `calc(${this.offsetX.substring(1)} + 6px)`;
                return undefined;
            },
            iconClass() {
                let cls = "ico po-ico";
                if (this.icon)
                    cls += ' ico-' + this.icon;
                return cls;
            },
            poClass() {
                return this.lineClamp ? 'line-clamp' : undefined;
            },
            poStyle() {
                return this.lineClamp ? { '-webkit-line-clamp': '' + this.lineClamp } : undefined;
            },
            visible() {
                return !!this.url && this.state === 'shown';
            },
            isShowHover() {
                return this.hover && this.hoverstate ? 'show' : undefined;
            }
        },
        methods: {
            mouseover() {
                if (this.hover)
                    this.hoverstate = true;
            },
            mouseout() {
                if (this.hover)
                    this.hoverstate = false;
            }
        },
        mounted() {
            this.$el._show = () => {
                this.state = 'shown';
                if (this.url) {
                    const urltools = require$1('std:url');
                    const root = window.$$rootUrl;
                    let arg = this.arg || '';
                    if (typeof arg === 'object')
                        arg = arg.Id;
                    this.popoverUrl = urltools.combine(root, '/_popup', this.url, arg);
                }
            };
            this.$el._hide = () => {
                this.state = 'hidden';
                this.popoverUrl = '';
            };
        }
    }));

    // a2-span-sum (Net48 components/spansum.js), unchanged in behaviour: the content and an arrow by dir: > 0 in (up,
    // green), < 0 out (down, red), 0 in-out; no dir, no arrow. Vue 3's h: domProps are plain props.
    platform$3.vocabulary.component('a2-span-sum', vue.defineComponent({
        props: {
            content: [String, Number],
            dir: Number
        },
        render() {
            const children = [];
            children.push(vue.h('span', { innerText: this.content }));
            let dcls = 'span-sum ';
            if (this.dir > 0) { /* in */
                children.push(vue.h('i', { 'class': 'ico ico-arrow-up-green' }));
                dcls += 'in';
            }
            else if (this.dir < 0) { /* out */
                children.push(vue.h('i', { 'class': 'ico ico-arrow-down-red' }));
                dcls += 'out';
            }
            else if (this.dir === 0) { /* inout */
                children.push(vue.h('i', { 'class': 'ico ico-arrow-sort' }));
                dcls += 'inout';
            }
            return vue.h('span', { class: dcls }, children);
        }
    }));

    // a2-inline-dialog (Net48 components/inlinedialog.js), unchanged in behaviour: a modal in the page's markup, opened and
    // closed by eventBus 'inlineDialog' {cmd: 'open' | 'close' | 'count', id} (the controller's $inlineOpen, $inlineClose,
    // $inlineDepth). The open ones are a stack: Esc closes the top one; no close while requests run (__requestsCount__).
    // beforeDestroy is beforeUnmount.
    const eventBus$1 = require$1('std:eventBus');
    const __inlineStack = []; // for ESC support
    platform$3.vocabulary.component('a2-inline-dialog', vue.defineComponent({
        template: `<div class="inline-modal-wrapper modal-animation-frame" v-if="visible" :class="{show: open}" v-cloak>
	<div class="modal-window modal-animation-window" :class="{loaded: open}" :style="dlgStyle">
		<slot></slot>
	</div>
</div>
`,
        props: {
            dialogId: String,
            dialogTitle: String,
            width: String,
            noClose: Boolean
        },
        data() {
            return {
                visible: false,
                open: false //for animation
            };
        },
        computed: {
            dlgStyle() {
                if (this.width)
                    return { width: this.width };
                return undefined;
            }
        },
        methods: {
            __keyUp(event) {
                if (this.noClose)
                    return;
                if (event.which === 27) {
                    event.stopPropagation();
                    event.preventDefault();
                    if (__inlineStack.length && __inlineStack[0] !== this.dialogId)
                        return;
                    setTimeout(() => {
                        eventBus$1.$emit('inlineDialog', { cmd: 'close', id: this.dialogId });
                    }, 1);
                }
            },
            __inlineEvent(opts) {
                if (!opts)
                    return;
                if (opts.id !== this.dialogId)
                    return;
                switch (opts.cmd) {
                    case 'close':
                        if (window.__requestsCount__ > 0)
                            return;
                        __inlineStack.shift();
                        this.open = false;
                        this.visible = false;
                        document.removeEventListener('keyup', this.__keyUp);
                        break;
                    case 'open':
                        this.visible = true;
                        __inlineStack.unshift(this.dialogId);
                        document.addEventListener('keyup', this.__keyUp);
                        setTimeout(() => {
                            this.open = true;
                        }, 50); // same as shell
                        break;
                    case 'count':
                        opts.count = __inlineStack.length;
                        break;
                    default:
                        console.error(`invalid inline command '${opts.cmd}'`);
                }
            },
            __inlineCount(opts) {
                opts.count = __inlineStack.length;
            }
        },
        created() {
            eventBus$1.$on('inlineDialog', this.__inlineEvent);
            eventBus$1.$on('inlineDialogCount', this.__inlineCount);
        },
        beforeUnmount() {
            document.removeEventListener('keyup', this.__keyUp);
            eventBus$1.$off('inlineDialog', this.__inlineEvent);
            eventBus$1.$off('inlineDialogCount', this.__inlineCount);
        }
    }));

    // a2-kanban (Net48 components/kanban.js), unchanged in behaviour: lanes, and in each the items whose item[stateProp]
    // is the lane (by $id); slots header and footer take the lane, card the item. A card is dragged (HTML5 drag and drop)
    // onto a lane: dropDelegate(card, lane); onto the trash: trashDelegate(card). No dropDelegate, no drag.
    const template = `
<div class="kanban">
	<div class="kanban-header kanban-part">
		<div class=lane-header v-for="(lane, lx) in lanes" :key=lx :class="laneClass(lane)">
			<div class=lane-header-body>
				<slot name=header v-bind:lane=lane></slot>
			</div>
			<button v-if="false">›</button>
		</div>
		<div class="kanban-trash drop-shadow shadow1" v-if="showTrash" @dragover="dragTrash($event)"
				@drop="dropTrash($event)" :class="trashClass()" @dragleave="clearDrag($event)">
			<i class="ico ico-trash"></i>
		</div>
	</div>
	<div class="kanban-body kanban-part">
		<div class=lane v-for="(lane, lx) in lanes" :key=lx @dragover="dragOver($event, lane)"
				@drop="drop($event, lane)" :class="laneClass(lane)" @dragleave="clearDrag($event)">
			<ul class=card-list>
				<li class=card v-for="(card, cx) in cards(lane)" :key=cx :draggable="true"
						@dragstart="dragStart($event, card)" @dragend=dragEnd>
					<slot name=card v-bind:card=card></slot>
				</li>
			</ul>
		</div>
	</div>
	<div class="kanban-footer kanban-part">
		<div class=lane-footer  v-for="(lane, lx) in lanes" :key=lx>
			<slot name=footer v-bind:lane=lane></slot>
		</div>
	</div>
</div>
`;
    platform$3.vocabulary.component('a2-kanban', vue.defineComponent({
        template,
        props: {
            lanes: Array,
            items: Array,
            dropDelegate: Function,
            trashDelegate: Function,
            stateProp: String,
            showTrash: Boolean
        },
        data() {
            return {
                currentElem: null,
                laneOver: null,
                insideTrash: false
            };
        },
        methods: {
            cards(lane) {
                const id = lane.$id;
                return this.items.filter(itm => itm[this.stateProp].$id === id);
            },
            clearDrag(ev) {
                if (ev && ev.currentTarget.contains(ev.relatedTarget))
                    return;
                this.laneOver = null;
                this.insideTrash = false;
            },
            dragEnd() {
                this.clearDrag();
            },
            dragStart(ev, card) {
                this.clearDrag();
                if (!this.dropDelegate) {
                    ev.preventDefault();
                    return;
                }
                ev.dataTransfer.effectAllowed = "move";
                this.currentElem = card;
            },
            dragOver(ev, lane) {
                this.laneOver = lane;
                this.insideTrash = false;
                ev.preventDefault();
            },
            dragTrash(ev) {
                this.insideTrash = true;
                ev.preventDefault();
            },
            drop(ev, lane) {
                this.clearDrag();
                if (!this.currentElem)
                    return;
                if (this.dropDelegate)
                    this.dropDelegate(this.currentElem, lane);
                this.currentElem = null;
            },
            dropTrash(ev) {
                this.clearDrag();
                if (!this.currentElem)
                    return;
                if (this.trashDelegate)
                    this.trashDelegate(this.currentElem);
                this.currentElem = null;
            },
            laneClass(lane) {
                return lane == this.laneOver ? 'over' : undefined;
            },
            trashClass() {
                return this.insideTrash ? 'over' : undefined;
            }
        }
    }));

    // a2-time-picker (Net48 components/timepicker.js), unchanged in behaviour: over control; the time of item[prop] typed,
    // or picked on a sheet (an hour, then minutes by 5, which closes it); the date is kept, a zero date is today's on
    // open. A popup. beforeDestroy is beforeUnmount. The sheet's headers lose their text under v-text (Години, Хвилини):
    // Vue 2's v-text replaced it, Vue 3's compiler refuses it.
    const popup = require$1('std:popup');
    const utils$2 = require$1('std:utils');
    const eventBus = require$1('std:eventBus');
    const locale$2 = window.$$locale;
    const dateLocale$1 = locale$2.$DateLocale || locale$2.$Locale;
    const timesheet = vue.defineComponent({
        props: {
            model: Date,
            setHour: Function,
            setMinute: Function,
            getHourClass: Function,
            getMinuteClass: Function
        },
        template: `
<div @click.stop.prevent="dummy" class="time-picker-pane calendar-pane">
<table class="table-hours">
<thead><tr><th colspan="6" v-text="locale.$Hours"></th></tr></thead>
<tbody>
	<tr v-for="row in hours">
		<td v-for="h in row" :class="getHourClass(h)"><a @click.stop.prevent="clickHours(h)" v-text="h"/></td>
	</tr>
</tbody></table>
<div class="divider"/>
<table class="table-minutes">
<thead><tr><th colspan="3" v-text="locale.$Minutes"></th></tr></thead>
<tbody>
	<tr v-for="row in minutes">
		<td v-for="m in row" :class="getMinuteClass(m)"><a @click.stop.prevent="clickMinutes(m)" v-text="m"/></td>
	</tr>
</tbody></table>
</div>
`,
        methods: {
            clickHours(h) {
                if (this.setHour)
                    this.setHour(+h);
            },
            clickMinutes(m) {
                if (this.setMinute)
                    this.setMinute(m);
            },
            dummy() {
            }
        },
        computed: {
            locale() { return locale$2; },
            hours() {
                const a = [];
                for (let y = 0; y < 4; y++) {
                    const r = [];
                    a.push(r);
                    for (let x = 0; x < 6; x++) {
                        let v = y * 6 + x;
                        if (v < 10)
                            v = '0' + v;
                        r.push(v);
                    }
                }
                return a;
            },
            minutes() {
                const a = [];
                for (let y = 0; y < 4; y++) {
                    const r = [];
                    a.push(r);
                    for (let x = 0; x < 3; x++) {
                        let v = (y * 3 + x) * 5;
                        if (v < 10)
                            v = '0' + v;
                        r.push(v);
                    }
                }
                return a;
            }
        }
    });
    platform$3.vocabulary.component('a2-time-picker', vue.defineComponent({
        extends: control,
        template: `
<div :class="cssClass2()" class="date-picker">
	<label v-if="hasLabel"><span v-text="label"/><slot name="hint"/><slot name="link"></slot></label>
	<div class="input-group">
		<input v-focus v-model.lazy="model" :class="inputClass" :disabled="inputDisabled"/>
		<a href @click.stop.prevent="toggle($event)"><i class="ico ico-waiting-outline"></i></a>
		<validator :invalid="invalid" :errors="errors" :options="validatorOptions"></validator>
		<div class="calendar" v-if="isOpen">
			<a2-time-sheet :model="modelDate" :set-hour="setHour" :set-minute="setMinute" :get-hour-class="hourClass" :get-minute-class="minuteClass"/>
		</div>
	</div>
	<span class="descr" v-if="hasDescr" v-text="description"></span>
</div>
`,
        components: {
            'a2-time-sheet': timesheet
        },
        props: {
            item: Object,
            prop: String,
            itemToValidate: Object,
            propToValidate: String,
            // override control.align (default value)
            align: { type: String, default: 'center' }
        },
        data() {
            return {
                isOpen: false
            };
        },
        methods: {
            toggle() {
                if (this.disabled)
                    return;
                if (!this.isOpen) {
                    // close other popups
                    eventBus.$emit('closeAllPopups');
                    if (utils$2.date.isZero(this.modelDate))
                        this.item[this.prop] = utils$2.date.today();
                }
                this.isOpen = !this.isOpen;
            },
            setHour(h) {
                const nd = new Date(this.modelDate);
                nd.setHours(h);
                this.item[this.prop] = nd;
            },
            setMinute(m) {
                const md = new Date(this.modelDate);
                md.setMinutes(m);
                this.item[this.prop] = md;
                this.isOpen = false;
            },
            hourClass(h) {
                let cls = '';
                if (this.modelDate.getHours() === +h)
                    cls += ' active';
                return cls;
            },
            minuteClass(m) {
                return this.modelDate.getMinutes() === +m ? 'active' : undefined;
            },
            cssClass2() {
                let cx = this.cssClass();
                if (this.isOpen)
                    cx += ' open';
                return cx;
            },
            __clickOutside() {
                this.isOpen = false;
            }
        },
        computed: {
            modelDate() {
                return this.item[this.prop];
            },
            inputDisabled() {
                return this.disabled;
            },
            model: {
                get() {
                    const md = this.modelDate;
                    if (utils$2.date.isZero(md))
                        return '';
                    return md.toLocaleTimeString(dateLocale$1, { hour: '2-digit', minute: "2-digit" });
                },
                set(str) {
                    let md = new Date(this.modelDate);
                    if (str) {
                        if (utils$2.date.isZero(md))
                            md = utils$2.date.today();
                        const time = utils$2.date.parseTime(str);
                        md.setHours(time.getHours(), time.getMinutes());
                    }
                    else {
                        md.setHours(0, 0);
                    }
                    this.item[this.prop] = md;
                    this.isOpen = false;
                }
            }
        },
        mounted() {
            popup.registerPopup(this.$el);
            this.$el._close = this.__clickOutside;
        },
        beforeUnmount() {
            popup.unregisterPopup(this.$el);
        }
    }));

    // a2-dashboard, a2-dashboard-item (Net48 components/dashboard.js), unchanged in behaviour: items on a grid of cells
    // (row, col, rowSpan, colSpan); in editMode the free cells are placeholders, an item is dragged (HTML5 drag and drop)
    // to a free place, a new one from the list (grouped by groupBy) beside, and removed by its ✕. The item and the
    // placeholder reach the board by $parent, as Net48. Vue 3 changes:
    // - the placeholders' v-for and v-if are on two elements, the key on the placeholder: Vue 3 wants a <template v-for>'s
    //   key on the template, and gives v-if priority over v-for;
    // - a placeholder is reactive: $hover sets its hover, which Vue 2 rendered when lastPhRow/lastPhCol changed with it,
    //   Vue 3 only when the computed rows/cols change their value;
    // - editMode: Vue 2's prop option `false` is `null` (any).
    const itemTemplate = `
<div class=dashboard-item :draggable="editMode"
	:style="{gridArea: gridArea}" @dragstart=dragStart @dragend=dragEnd>
	<slot></slot>
	<div class=drag-area v-if=editMode>
		<button v-if="!isnew" class="clear-button" @click=remove>✕</button>
	</div>
</div>
`;
    const placeholderTemplate = `
<div class='dashboard-placeholder' @drop=drop @dragover=dragOver @dragenter=dragEnter
:style="{gridRow: row, gridColumn: col}" :class="{hover}"></div>
`;
    const boardTemplate = `
<div class="dashboard-container" :class="{editing: editMode}">
	<div class="drag-host" ref=drag-host></div>
	<div class=dashboard :style="{gridTemplateColumns: templateColumns, gridTemplateRows: templateRows}" ref=dash>
		<div v-if="editable && !editMode" class="start-toolbar toolbar"
				:style="{'grid-column':cols + 1}" :class="{'no-items': !hasItems}">
			<slot name="startbtn"></slot>
		</div>
		<div v-if="!editMode" class="main-toolbar"
			:style="{'grid-column':cols + 1}">
			<slot name="toolbar2"></slot>
		</div>
		<template v-if=editMode>
			<a2-dashboard-placeholder v-for="ph in placeholders" v-show="placeholderVisible(ph.row, ph.col)"
				:row="ph.row" :col="ph.col" :key="ph.key" :hover="ph.hover"/>
		</template>
		<slot>
			<a2-dashboard-item v-for="(itm, ix) in items" :key=ix :item="itm"
					:edit-mode="editMode"
					:row="itm.row" :col="itm.col" :col-span="itm.colSpan" :row-span="itm.rowSpan">
				<slot name="element" v-bind:item="itm"></slot>
			</a2-dashboard-item>
		</slot>
		<div v-if="!hasItems && !editMode">
			<slot name="empty"></slot>
		</div>
	</div>
	<div class="dashboard-list" v-if="editMode">
		<div class="widget-toolbar">
			<slot name="toolbar"></slot>
		</div>
		<ul class="widget-list-grouping" v-if="groupBy">
			<li class="widget-group" v-for="(grp, ixg) in groupingList" :key="ixg"
					:class="{collapsed: isGroupCollapsed(grp)}">
				<div class="widget-group-title" @click.stop.prevent="toggleGroup(grp)">
					<span v-text="grp.name"/>
					<span class="ico collapse-handle"></span>
				</div>
				<ul class="widget-list">
					<a2-dashboard-item v-for="(itm, ix) in grp.items" :key=ix :edit-mode="true"
							:item=itm :col-span="itm.colSpan" :row-span="itm.rowSpan" :isnew=true>
						<slot name="listitem" v-bind:item="itm"></slot>
					</a2-dashboard-item>
				</ul>
			</li>
		</ul>
		<ul class="widget-list" v-else>
			<a2-dashboard-item v-for="(itm, ix) in list" :key=ix :edit-mode="true"
					:item=itm :col-span="itm.colSpan" :row-span="itm.rowSpan" :isnew=true>
				<slot name="listitem" v-bind:item="itm"></slot>
			</a2-dashboard-item>
		</ul>
	</div>
</div>
`;
    const placeHolder = vue.defineComponent({
        name: 'a2-dashboard-placeholder',
        template: placeholderTemplate,
        props: {
            row: Number,
            col: Number,
            hover: Boolean
        },
        computed: {
            obj() {
                return { row: this.row, col: this.col };
            }
        },
        methods: {
            dragOver(ev) {
                if (this.$parent.$canDrop(this.obj))
                    ev.preventDefault();
            },
            dragEnter() {
                this.$parent.$enter(this.obj);
            },
            drop() {
                this.$parent.$drop(this.obj);
            }
        }
    });
    platform$3.vocabulary.component('a2-dashboard-item', vue.defineComponent({
        template: itemTemplate,
        props: {
            row: { type: Number, default: 1 },
            rowSpan: { type: Number, default: 1 },
            col: { type: Number, default: 1 },
            colSpan: { type: Number, default: 1 },
            isnew: Boolean,
            item: Object,
            editMode: Boolean
        },
        data() {
            return {
                posX: 0,
                posY: 0
            };
        },
        computed: {
            gridArea() {
                return `${this.row} / ${this.col} / span ${this.rowSpan} / span ${this.colSpan}`;
            }
        },
        methods: {
            dragStart(ev) {
                if (!this.editMode)
                    return;
                ev.dataTransfer.effectAllowed = "move";
                if (this.isnew) {
                    this.posX = 0;
                    this.posY = 0;
                }
                else {
                    const target = ev.target;
                    this.posY = Math.floor(ev.offsetY / (target.offsetHeight / this.rowSpan));
                    this.posX = Math.floor(ev.offsetX / (target.offsetWidth / this.colSpan));
                }
                const img = this.$parent.$getDragImage(this);
                ev.dataTransfer.setDragImage(img, ev.offsetX, ev.offsetY);
                this.$parent.$start(this);
            },
            dragEnd() {
                if (!this.editMode)
                    return;
                this.$parent.$dragEnd();
            },
            remove() {
                this.$parent.$removeItem(this.item);
            }
        },
        mounted() {
            this.$parent.$register(this);
        }
    }));
    platform$3.vocabulary.component('a2-dashboard', vue.defineComponent({
        template: boardTemplate,
        components: {
            'a2-dashboard-placeholder': placeHolder
        },
        props: {
            items: Array,
            list: Array,
            groupBy: String,
            editable: Boolean,
            editMode: null,
            cellSize: {
                type: Object, default() { return { cx: '100px', cy: '100px' }; }
            },
        },
        data() {
            return {
                staticElems: [],
                currentElem: null,
                lastPhRow: 0,
                lastPhCol: 0,
                collapsedGroups: []
            };
        },
        computed: {
            hasItems() {
                return !!(this.items && this.items.length);
            },
            groupingList() {
                if (!this.groupBy)
                    return [];
                const el = [];
                this.list.forEach(p => {
                    const g = p[this.groupBy];
                    const found = el.find(x => x.name === g);
                    if (found)
                        found.items.push(p);
                    else
                        el.push({
                            name: g,
                            items: [p]
                        });
                });
                return el;
            },
            rows() {
                let rows = 0;
                const items = this.items;
                if (items && items.length)
                    rows = items.reduce((p, c) => Math.max(p, c.row + (c.rowSpan || 1) - 1), -Infinity);
                if (this.editMode) {
                    rows += 1;
                    rows = Math.max(rows, this.lastPhRow);
                }
                return rows;
            },
            cols() {
                let cols = 0;
                const items = this.items;
                if (items && items.length)
                    cols = items.reduce((p, c) => Math.max(p, c.col + (c.colSpan || 1) - 1), -Infinity);
                if (this.editMode) {
                    cols += 1;
                    cols = Math.max(cols, this.lastPhCol);
                }
                return cols;
            },
            placeholders() {
                const ph = [];
                for (let r = 0; r < this.rows; r++) {
                    for (let c = 0; c < this.cols; c++) {
                        ph.push(vue.reactive({ row: r + 1, col: c + 1, key: `${c}:${r}`, hover: false }));
                    }
                }
                return ph;
            },
            templateColumns() {
                if (!this.hasItems && !this.editMode)
                    return '';
                return `repeat(${this.cols}, ${this.cellSize.cx}) ${this.editable ? 'minmax(20px,1fr)' : ''}`;
            },
            templateRows() {
                if (!this.hasItems && !this.editMode)
                    return '';
                return `repeat(${this.rows}, ${this.cellSize.cy})`;
            },
            elements() {
                if (this.items)
                    return this.items.map(item => {
                        return {
                            startRow: item.row,
                            startCol: item.col,
                            endRow: item.row + (item.rowSpan || 1) - 1,
                            endCol: item.col + (item.colSpan || 1) - 1
                        };
                    });
                return this.staticElems;
            }
        },
        methods: {
            placeholderVisible(row, col) {
                if (!this.editMode)
                    return false;
                const intercect = (elem) => row >= elem.startRow && row <= elem.endRow &&
                    col >= elem.startCol && col <= elem.endCol;
                return !this.elements.find(intercect);
            },
            $register(item) {
                this.staticElems.push({ startRow: item.row, startCol: item.col, endRow: item.row + item.rowSpan - 1, endCol: item.col + item.colSpan - 1 });
            },
            $removeItem(itm) {
                this.items.$remove(itm);
                this.$clearHover();
                this.lastPhRow = 0;
                this.lastPhCol = 0;
            },
            $findPlaceholder(el) {
                return this.placeholders.find((x) => x.row === el.row && x.col == el.col);
            },
            $hover(arr, pos) {
                this.lastPhRow = pos.y + pos.cy;
                this.lastPhCol = pos.x + pos.cx;
                this.placeholders.forEach((ph) => {
                    const sign = `${ph.row}:${ph.col}`;
                    const find = arr.find(ai => ai === sign);
                    ph.hover = !!find;
                });
            },
            $dragEnd() {
                this.$clearHover();
                this.lastPhRow = 0;
                this.lastPhCol = 0;
            },
            $clearHover() {
                this.placeholders.forEach((ph) => ph.hover = false);
            },
            $start(el) {
                this.currentElem = el;
            },
            $getDragImage(el) {
                const img = this.$refs['drag-host'];
                const rs = window.getComputedStyle(this.$refs.dash);
                const colSize = parseFloat(rs.gridTemplateColumns.split(' ')[0]);
                const rowSize = parseFloat(rs.gridTemplateRows.split(' ')[0]);
                const colGap = parseFloat(rs.columnGap);
                const rowGap = parseFloat(rs.rowGap);
                img.style.width = (colSize * el.colSpan + (el.colSpan - 1) * colGap) + 'px';
                img.style.height = (rowSize * el.rowSpan + (el.rowSpan - 1) * rowGap) + 'px';
                return img;
            },
            $setHover(el) {
                const ce = this.currentElem;
                if (!ce)
                    return;
                const ph = this.$findPlaceholder(el);
                if (!ph)
                    return;
                const x = ph.col - ce.posX;
                const y = ph.row - ce.posY;
                const arr = [];
                for (let r = 0; r < ce.rowSpan; r++)
                    for (let c = 0; c < ce.colSpan; c++)
                        arr.push(`${r + y}:${c + x}`);
                this.$hover(arr, { x, y, cx: ce.colSpan, cy: ce.rowSpan });
            },
            $enter(el) {
                this.$setHover(el);
            },
            $canDrop(el) {
                const ce = this.currentElem;
                if (!ce)
                    return false;
                const pos = {
                    t: el.row - ce.posY,
                    l: el.col - ce.posX,
                    b: 0,
                    r: 0
                };
                pos.b = pos.t + ce.rowSpan - 1;
                pos.r = pos.l + ce.colSpan - 1;
                const intersect = (el) => {
                    const r = {
                        t: el.row,
                        l: el.col,
                        b: el.row + (el.rowSpan || 1) - 1,
                        r: el.col + (el.colSpan || 1) - 1
                    };
                    const ints = r.l > pos.r ||
                        r.r < pos.l ||
                        r.t > pos.b ||
                        r.b < pos.t;
                    return !ints;
                };
                // check intersections here
                return !this.items.some(el => el !== ce.item && intersect(el));
            },
            $drop(el) {
                this.$clearHover();
                const ce = this.currentElem;
                if (ce) {
                    if (ce.item) {
                        ce.item.row = el.row - ce.posY;
                        ce.item.col = el.col - ce.posX;
                        if (ce.isnew)
                            this.items.push(Object.assign({}, ce.item));
                    }
                    this.currentElem = null;
                }
            },
            isGroupCollapsed(grp) {
                return this.collapsedGroups.indexOf(grp.name) >= 0;
            },
            toggleGroup(grp) {
                if (!this.isGroupCollapsed(grp))
                    this.collapsedGroups.push(grp.name);
                else {
                    const ix = this.collapsedGroups.indexOf(grp.name);
                    if (ix >= 0)
                        this.collapsedGroups.splice(ix, 1);
                }
            }
        },
        watch: {
            editMode(val) {
                if (!val) {
                    this.lastPhRow = 0;
                    this.lastPhCol = 0;
                }
            }
        }
    }));

    // a2-file-preview (Net48 components/image.js), unchanged in behaviour: the file value (an object with Mime) at
    // /_file/<url>/<id>?token=…, an <img> for image/*, an <object> for a pdf, else "preview is unavailable". The other
    // components of image.js (a2-image over a2-upload, a2-file-image, a2-static-image) are not ported yet.
    // Net48's template had a stray quote after v-if="isImage" and v-else-if="isPdf": Vue 2's parser took it, Vue 3's
    // compiler refuses it; dropped.
    const url = require$1('std:url');
    const utils$1 = require$1('std:utils');
    const locale$1 = window.$$locale;
    platform$3.vocabulary.component('a2-file-preview', vue.defineComponent({
        template: `<div class="a2-file-preview">
		<img v-if="isImage" :src=href :style=cssStyle />
		<object v-else-if="isPdf" type="application/pdf" :data=href
			:width=pdfWidth :height=height :style=cssStyle></object>
		<div v-else class="no-preview">
			<span class="ico ico-file-preview"></span>
			<span v-text="unavailable"></span>
		</div>
	</div>`,
        props: {
            url: String,
            width: String,
            height: String,
            value: Object
        },
        computed: {
            mime() {
                if (utils$1.isObjectExact(this.value))
                    return this.value.Mime;
                console.error('value must be an Object');
                return undefined;
            },
            isImage() {
                return this.mime.startsWith('image');
            },
            isPdf() {
                return this.mime.endsWith('pdf');
            },
            pdfWidth() {
                return this.width ?? '100%';
            },
            href() {
                const root = window.$$rootUrl;
                const value = this.value;
                let id = value;
                const qry = {};
                if (utils$1.isObjectExact(value)) {
                    id = utils$1.getStringId(value);
                    if (value._meta_ && value._meta_.$token)
                        qry.token = value[value._meta_.$token];
                }
                return url.combine(root, '_file', this.url, id) + url.makeQueryString(qry);
            },
            unavailable() {
                return locale$1.$PreviewIsUnavailable;
            },
            cssStyle() {
                const r = {};
                if (this.width)
                    r.maxWidth = this.width;
                if (this.height)
                    r.maxHeight = this.height;
                return r;
            }
        }
    }));

    // a2-big-calendar (A2v10.Core Scripts/bigcalendar.js), unchanged in behaviour: events by a month or by a week of half
    // hours; item[prop] the date shown, viewItem[viewProp] the view ('week', 'month'); a click on a day, an hour or an
    // event goes to delegates.ClickDay, ClickEvent. In a week the current time's line is redrawn every 10 seconds.
    // destroyed is unmounted.
    const TODAY_COLOR = "#fffff080"; // sync with LESS!
    const locale = window.$$locale;
    const dateLocale = locale.$DateLocale || locale.$Locale;
    const monthLocale = locale.$Locale; // for text
    const utils = require$1('std:utils');
    const tu = utils.text;
    const du = utils.date;
    platform$3.vocabulary.component('a2-big-calendar', vue.defineComponent({
        template: `
<div class="a2-big-calendar">
<div class="top-bar">
	<div class="toolbar">
		<button class="btn btn-tb btn-icon" @click="nextPart(-1)"><i class="ico ico-arrow-left"></i></button>
		<button class="btn btn-tb btn-icon" @click="nextPart(1)"><i class="ico ico-arrow-right"></i></button>
		<button class="btn btn-tb btn-icon" @click="todayPart"><i class="ico ico-calendar-today"></i> <span v-text="locale.$Today"></span></button>
		<slot name="leftbar"></slot>
	</div>
	<div class="title">
		<span v-text=topTitle></span>
	</div>
	<div class="toolbar">
		<button class="btn btn-tb btn-icon" @click="setView('week')"><i class="ico ico-calendar-week"></i> <span v-text="locale.$Week"></span></button>
		<button class="btn btn-tb btn-icon" @click="setView('month')"><i class="ico ico-calendar"></i> <span v-text="locale.$Month"></span></button>
		<slot name="topbar"></slot>
	</div>
</div>
<div v-if="isView('month')" class="bc-month-conainer bc-container" ref="mc">
<table class="bc-month bc-table">
	<thead>
		<tr class="weekdays"><th v-for="d in 7" v-text="wdTitle(d)"></th></tr>
	</thead>
	<tbody>
		<tr v-for="row in days">
			<td v-for="day in row" class=bc-day :class="dayClass(day)" @click.stop.prevent="clickDay(day)">
				<div v-text="day.getDate()" class="day-date"></div>
				<div class="bc-day-body">
					<div v-for="(ev, ix) in dayEvents(day)" :key=ix class="day-event"
							:style="{backgroundColor: ev.Color || ''}" @click.stop.prevent="clickEvent(ev)">
						<slot name="monthev" v-bind:item="ev" class="me-body">
							<span class="me-body" v-text="eventTitle(ev)" :title="eventTitle(ev)"/>
						</slot>
					</div>
				</div>
			</td>
		</tr>
	</tbody>
</table>
</div>
<div v-if="isView('week')" class="bc-week-container bc-container" ref=wc>
	<table class="bc-week bc-table">
		<colgroup>
			<col style="width:2%"/>
			<col v-for="d in 7" style="width:14%" :style="weekColumnStyle(d)">
		</colgroup>
		<thead>
			<tr class="weekdays">
				<th></th>
				<th v-for="d in 7" v-text="wdWeekTitle(d)" :class="{today: isWeekDayToday(d)}"></th>
			</tr>
		</thead>
		<tbody>
			<template v-for="h in 24">
				<tr>
					<th rowspan=2>
						<span v-text="hoursText(h - 1)" class="h-title"></span>
					</th>
					<td v-for="d in 7" class="bc-h-top" @click.stop.prevent="clickHours(d, h, false)">
						<div class="h-event" v-for="(ev, ix) in hourEvents(d, h-1, false)" :key=ix
								:style="hourStyle(ev, false)" @click.stop.prevent="clickEvent(ev)">
							<slot name="weekev" v-bind:item="ev">
								<span class="h-ev-body" v-text="eventTitle(ev)" :title="eventTitle(ev)"></span>
							</slot>
						</div>
					</td>
				</tr>
				<tr>
					<td v-for="d in 7" class="bc-h-bottom" @click.stop.prevent="clickHours(d, h, true)">
						<div class="h-event" v-for="(ev, ix) in hourEvents(d, h-1, true)" :key=ix
							:style="hourStyle(ev, true)" @click.stop.prevent="clickEvent(ev)">
							<slot name="weekev" v-bind:item="ev">
								<span class="h-ev-body" v-text="eventTitle(ev)" :title="eventTitle(ev)"></span>
							</slot>
						</div>
					</td>
				</tr>
			</template>
		</tbody>
	</table>
	<div class="current-time" :style="currentTimeStyle()" :key=updateKey></div>
</div>
</div>
`,
        props: {
            item: Object,
            prop: String,
            viewItem: Object,
            viewProp: String,
            events: Array,
            delegates: Object
        },
        data() {
            return {
                updateKey: 1,
                timerId: 0
            };
        },
        computed: {
            locale() {
                return locale;
            },
            modelDate() {
                return this.item[this.prop];
            },
            modelView() {
                return this.viewItem[this.viewProp];
            },
            days() {
                const dt = new Date(this.modelDate);
                dt.setDate(1); // 1-st day of month
                let w = dt.getDay() - 1; // weekday
                if (w === -1)
                    w = 6;
                dt.setDate(-w + 1);
                const arr = [];
                for (let r = 0; r < 6; r++) {
                    const row = [];
                    for (let c = 0; c < 7; c++) {
                        row.push(new Date(dt));
                        dt.setDate(dt.getDate() + 1);
                    }
                    arr.push(row);
                }
                return arr;
            },
            topTitle() {
                const m = this.modelDate.toLocaleDateString(monthLocale, { month: "long" });
                return `${tu.capitalize(m)} ${this.modelDate.getFullYear()}`;
            },
            firstMonday() {
                let wd = this.modelDate.getDay();
                if (wd == 1)
                    return this.modelDate;
                if (!wd)
                    wd = 7;
                return du.add(this.modelDate, -(wd - 1), 'day');
            }
        },
        methods: {
            isView(view) {
                return this.viewItem[this.viewProp] === view;
            },
            wdTitle(d) {
                const dt = this.days[0][d - 1];
                return dt.toLocaleString(monthLocale, { weekday: "long" });
            },
            wdWeekTitle(d) {
                const fd = du.add(this.firstMonday, d - 1, 'day');
                const wd = fd.toLocaleString(monthLocale, { weekday: 'long' });
                const day = fd.toLocaleString(monthLocale, { month: "long", day: 'numeric' });
                return `${wd}, ${day}`;
            },
            dayClass(day) {
                let cls = '';
                if (du.equal(day, du.today()))
                    cls += ' today';
                if (day.getMonth() !== this.modelDate.getMonth())
                    cls += " other";
                return cls;
            },
            dayEvents(day) {
                return this.events.filter(e => du.equal(e.Date, day));
            },
            hoursText(h) {
                return `${h}:00`;
            },
            hourStyle(ev, h2) {
                const min = ev.Date.getMinutes();
                const s = {
                    backgroundColor: ev.Color,
                    height: `${ev.Duration}px`,
                    top: `${h2 ? min - 30 : min}px`
                };
                return s;
            },
            hourEvents(dno, hour, h2) {
                const day = du.add(this.firstMonday, dno - 1, 'day');
                const inside = (ev) => {
                    if (!du.equal(ev.Date, day))
                        return false;
                    const h = ev.Date.getHours();
                    const m = ev.Date.getMinutes();
                    if (h2)
                        return h >= hour && h < hour + 1 && m >= 30;
                    else
                        return h >= hour && h < hour + 1 && m < 30;
                };
                return this.events.filter(inside);
            },
            nextPart(d) {
                let dt = new Date(this.modelDate);
                if (this.isView('week'))
                    dt = du.add(dt, 7 * d, 'day');
                else
                    dt.setMonth(dt.getMonth() + d);
                this.item[this.prop] = dt;
            },
            todayPart() {
                const dt = new Date();
                this.item[this.prop] = dt;
            },
            setView(view) {
                this.viewItem[this.viewProp] = view;
            },
            clickEvent(ev) {
                if (this.delegates && this.delegates.ClickEvent)
                    this.delegates.ClickEvent(ev);
            },
            clickDay(day) {
                if (!this.delegates || !this.delegates.ClickDay)
                    return;
                const dt = new Date(day);
                dt.setHours(0);
                dt.setMinutes(0);
                this.delegates.ClickDay(day);
            },
            clickHours(d, h, h2) {
                if (!this.delegates || !this.delegates.ClickDay)
                    return;
                const fmd = this.firstMonday;
                const dt = du.add(fmd, d - 1, 'day');
                dt.setMinutes(h2 ? 30 : 0);
                dt.setHours(h - 1);
                this.delegates.ClickDay(dt);
            },
            currentTimeStyle() {
                const dt = new Date();
                const x = dt.getHours() * 60 + dt.getMinutes() + 35 - 1 /*thead*/;
                return {
                    top: `${x}px`,
                    '--time': `"${dt.toLocaleTimeString(dateLocale, { hour: '2-digit', minute: '2-digit' })}"`
                };
            },
            isWeekDayToday(d) {
                const day = du.add(this.firstMonday, d - 1, 'day');
                return du.equal(day, du.today());
            },
            weekColumnStyle(d) {
                if (this.isWeekDayToday(d))
                    return { backgroundColor: TODAY_COLOR };
                return undefined;
            },
            eventTitle(ev) {
                const time = ev.Date.toLocaleTimeString(dateLocale, { hour: '2-digit', minute: '2-digit' });
                return `${time} ${ev.Name}`;
            },
            __fitScroll() {
                if (this.isView('month')) {
                    setTimeout(() => {
                        this.$refs.mc.scrollTop = 0;
                    }, 0);
                }
                else
                    setTimeout(() => {
                        const rows = this.$refs.wc.getElementsByTagName('tr');
                        rows[8 * 2 + 1].scrollIntoView(true);
                    }, 0);
            }
        },
        watch: {
            modelView() {
                this.__fitScroll();
            }
        },
        mounted() {
            this.__fitScroll();
            this.timerId = setInterval(() => {
                if (this.isView('week')) {
                    this.updateKey++;
                }
            }, 10 * 1000);
        },
        unmounted() {
            if (this.timerId)
                clearInterval(this.timerId);
        }
    }));

})(a2v10.platform, a2v10.platform.Vue);
//# sourceMappingURL=a2v10-xaml.js.map
