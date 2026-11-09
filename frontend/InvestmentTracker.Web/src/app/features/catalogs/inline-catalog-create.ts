import { Component, DestroyRef, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EditPanel } from '../../shared/edit-panel';
import { UnsavedForm } from '../../shared/unsaved-changes';
import { apiError } from '../../core/services/api-error';
import { CatalogService } from './catalog.service';
import { CatalogItem, CatalogKey, catalogs } from './catalog.models';

@Component({ selector: 'app-inline-catalog-create', standalone: true,
  imports: [EditPanel, UnsavedForm, ReactiveFormsModule, MatButtonModule, MatCheckboxModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  template: `<app-edit-panel [editing]="true" [busy]="saving()" [error]="error()" [title]="'Criar ' + config().singular" (closed)="closed.emit()">
    <form class="panel" [formGroup]="form" appUnsavedChanges [appFormBusy]="saving()" (ngSubmit)="save()"><fieldset [disabled]="saving()">
      <p>Crie a opção que falta e retorne ao ativo com o preenchimento preservado. Este cadastro será salvo agora; o ativo ainda precisa ser confirmado.</p>
      <mat-form-field appearance="outline"><mat-label>Nome do cadastro</mat-label><input matInput formControlName="name" maxlength="100" /></mat-form-field>
      @if (catalog() === 'currencies') {
        <mat-form-field appearance="outline"><mat-label>Código da moeda</mat-label><input matInput formControlName="code" maxlength="3" placeholder="Ex.: EUR" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Símbolo da moeda</mat-label><input matInput formControlName="symbol" maxlength="10" /></mat-form-field>
      }
      @if (catalog() === 'asset-types') { <mat-form-field appearance="outline"><mat-label>Classificação do instrumento</mat-label><select matNativeControl formControlName="instrumentClass"><option value="general">Outros instrumentos (ações, ETFs e demais)</option><option value="fii">FII</option><option value="reit">REIT</option><option value="unclassified">Revisar classificação</option></select></mat-form-field> }
      @if (catalog() === 'sectors') { <mat-checkbox formControlName="isRealEstate">Setor imobiliário</mat-checkbox><p class="hint">FII e REIT recebem sugestões de aporte somente em setores marcados como imobiliários.</p> }
      @if (form.touched && form.invalid) { <p class="field-error">Confira nome e campos obrigatórios.</p> }
      <div class="actions"><button mat-flat-button type="submit">Criar e selecionar</button><button mat-stroked-button data-discard type="button" (click)="closed.emit()">Cancelar</button></div>
    </fieldset></form>
  </app-edit-panel>`,
  styles: ['mat-form-field { width:100%; } fieldset { min-width:0; }']
})
export class InlineCatalogCreate {
  readonly catalog = input.required<CatalogKey>(); readonly created = output<CatalogItem>(); readonly closed = output<void>();
  private readonly api = inject(CatalogService); private readonly destroyRef = inject(DestroyRef);
  readonly saving = signal(false); readonly error = signal('');
  readonly form = inject(FormBuilder).nonNullable.group({ name: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(100)]], code: [''], symbol: ['', Validators.maxLength(10)], instrumentClass: ['general'], isRealEstate: [false] });
  config() { return catalogs.find(c => c.key === this.catalog())!; }
  save() {
    if (this.saving()) return;
    if (this.catalog() === 'currencies') this.form.controls.code.setValidators([Validators.required, Validators.pattern(/^[a-zA-Z]{3}$/)]);
    this.form.controls.code.updateValueAndValidity();
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue(); const body = this.catalog() === 'currencies' ? { name: v.name, code: v.code, symbol: v.symbol }
      : this.catalog() === 'asset-types' ? { name: v.name, instrumentClass: v.instrumentClass } : this.catalog() === 'sectors' ? { name: v.name, isRealEstate: v.isRealEstate } : { name: v.name };
    this.saving.set(true); this.error.set('');
    this.api.create(this.catalog(), body).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: value => { this.saving.set(false); this.created.emit(value); }, error: e => { this.saving.set(false); this.error.set(apiError(e)); } });
  }
}
