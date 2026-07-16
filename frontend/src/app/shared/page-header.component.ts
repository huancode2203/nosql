import { Component, input } from '@angular/core';
@Component({selector:'app-page-header',standalone:true,template:`<div class="page-header"><div><span class="eyebrow">{{eyebrow()}}</span><h1>{{title()}}</h1><p>{{subtitle()}}</p></div><ng-content/></div>`}) export class PageHeaderComponent{title=input.required<string>();subtitle=input('');eyebrow=input('EDUMANAGE LMS');}
