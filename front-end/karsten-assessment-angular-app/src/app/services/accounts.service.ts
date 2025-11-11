import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from './api.config';

export interface Account {
  id: number;
  name: string;
  account_number: string;
  balance: number;
}

export interface CreateAccountRequest {
  Name: string;
  AccountNumber: string;
}

export interface UpdateAccountRequest {
  Name: string;
  AccountNumber: string;
}

@Injectable({ providedIn: 'root' })
export class AccountsService {
  constructor(private http: HttpClient) {}

  getAccounts(): Observable<Account[]> {
    return this.http.get<Account[]>(`${API_URL}/accounts/get_all`);
  }

  createAccount(request: CreateAccountRequest): Observable<any> {
    return this.http.post(`${API_URL}/accounts/create`, request);
  }

  updateAccount(id: number, request: UpdateAccountRequest): Observable<any> {
    return this.http.put(`${API_URL}/accounts/${id}/update`, request);
  }
}