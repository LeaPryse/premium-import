import { Routes } from '@angular/router';
import { ImportPageComponent } from './features/import-and-view/import-page.component';

export const routes: Routes = [
  { path: '', pathMatch: 'full', component: ImportPageComponent },
  { path: '**', redirectTo: '' }
];
